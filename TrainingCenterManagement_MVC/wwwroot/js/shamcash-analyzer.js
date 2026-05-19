/* ShamCash QR analyzer orchestration */

let shamCashArabicWorkerPromise = null;

async function analyzeShamQr(input) {
    const file = input.files[0];
    if (!file) return;

    document.getElementById('shamQrLabelText').textContent = file.name;
    const statusEl = document.getElementById('shamQrStatus');
    const resultEl = document.getElementById('shamQrResult');
    const errorEl = document.getElementById('shamQrError');
    hideQrDebug();
    statusEl.style.display = 'flex';
    resultEl.style.display = 'none';
    errorEl.style.display = 'none';
    setQrStatus('جار تحليل الصورة...');

    try {
        const dataUrl = await readFileAsDataUrl(file);
        const img = await loadImageFromUrl(dataUrl);
        const canvas = drawImageToCanvas(img);

        // ── Step 1: Decode QR → serial + exact code boundary ────────────────
        let serial     = '';
        let nameFromQr = '';
        let qrBottomY  = 0;

        const imageData = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
        const qr = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: 'attemptBoth' });
        if (qr && qr.data) {
            try {
                const url = new URL(qr.data);
                const nameParam = url.searchParams.get('name') || url.searchParams.get('username');
                if (nameParam) nameFromQr = normalizeDetectedName(decodeURIComponent(nameParam));
            } catch (e) { /* not a URL */ }

            const hexMatch = qr.data.match(/[a-f0-9]{24,}/i);
            serial = hexMatch ? hexMatch[0] : qr.data.replace(/^.*[/=]/, '').trim();

            // jsQR gives us the exact pixel corners of the QR code
            if (qr.location) {
                qrBottomY = Math.max(
                    qr.location.bottomLeftCorner.y,
                    qr.location.bottomRightCorner.y
                );
            }
        }

        // ── Step 2: OCR the text region BELOW the QR code ────────────────────
        let name = normalizeDetectedName(nameFromQr);

        if (!name) {
            setQrStatus('قراءة الاسم من الصورة...');

            // Build binarized region (not scaled yet) so we can scan rows
            const binaryRegion = buildBinarizedTextRegion(canvas, qrBottomY);

            // Isolate the first text line (name).  If scan fails, crop top 45%.
            const nameLineCanvas = extractFirstTextLine(binaryRegion)
                || cropTopPortion(binaryRegion, 0.45);
            const ocrCanvas = scaleCanvasForOcr(nameLineCanvas);

            if (!window.Tesseract) {
                setQrStatus('تحميل محرك OCR...');
                await loadExternalScript('https://cdn.jsdelivr.net/npm/tesseract.js@4/dist/tesseract.min.js');
            }

            const worker = await getArabicOcrWorker('ara');

            // PSM 7 = single text line — best for a short Arabic name on one line
            await worker.setParameters({
                tessedit_pageseg_mode: 7,
                preserve_interword_spaces: '1',
                user_defined_dpi: '300'
            });
            const { data: { text: text7 } } = await worker.recognize(ocrCanvas);
            name = extractBestArabicName(text7);
            showQrDebug(ocrCanvas, text7);

            // Fallback: PSM 6 on the full text region (name + serial together)
            if (!name) {
                const fullOcr = scaleCanvasForOcr(binaryRegion);
                await worker.setParameters({ tessedit_pageseg_mode: 6,
                    preserve_interword_spaces: '1', user_defined_dpi: '300' });
                const { data: { text: text6 } } = await worker.recognize(fullOcr);
                showQrDebug(fullOcr, text6);
                name = extractBestArabicName(text6);

                // Last resort: line-by-line from PSM 6 output
                if (!name) {
                    for (const line of text6.split('\n').map(l => l.trim()).filter(l => l.length > 1)) {
                        if (/^[a-f0-9]{10,}$/i.test(line)) continue;
                        const cand = sanitizeArabicCandidate(line);
                        if (cand) { name = cand; break; }
                    }
                }
            }
        }

        applyDetectedValues(name, serial);
    } catch (err) {
        statusEl.style.display = 'none';
        showQrError('حدث خطأ أثناء التحليل: ' + err.message);
    }
}

/**
 * Crop the area below the QR code and binarize it.
 * Returns a 1:1 (unscaled) black-on-white canvas ready for line scanning.
 */
function buildBinarizedTextRegion(canvas, qrBottomY) {
    const w = canvas.width;
    const h = canvas.height;

    let textTop;
    if (qrBottomY > h * 0.35) {
        textTop = Math.floor(qrBottomY) + Math.floor(h * 0.015);
    } else {
        textTop = Math.floor(h * 0.80);
    }
    if (h - textTop < 20) textTop = Math.floor(h * 0.80);

    const textHeight = h - textTop;
    const tc = document.createElement('canvas');
    tc.width  = w;
    tc.height = textHeight;
    const tctx = tc.getContext('2d');
    tctx.drawImage(canvas, 0, textTop, w, textHeight, 0, 0, w, textHeight);

    // Auto-detect background: avg brightness > 120 → light bg (dark text, no invert)
    const sample = tctx.getImageData(0, 0, w, textHeight);
    let total = 0;
    for (let i = 0; i < sample.data.length; i += 4)
        total += (sample.data[i] + sample.data[i + 1] + sample.data[i + 2]) / 3;
    const shouldInvert = (total / (w * textHeight)) < 120;

    binarizeCanvas(tctx, w, textHeight, shouldInvert);
    return tc;
}

/**
 * Scan pixel rows of a binarized (dark-on-white) canvas to isolate the FIRST
 * text line (the name).  Requires 3+ consecutive near-empty rows to confirm a
 * real inter-line gap, so Arabic dots below letters don't cause false breaks.
 */
function extractFirstTextLine(binaryCanvas) {
    const w = binaryCanvas.width;
    const h = binaryCanvas.height;
    const data = binaryCanvas.getContext('2d').getImageData(0, 0, w, h).data;

    const TEXT_RATIO  = 0.010; // ≥1.0% dark pixels → text row
    const EMPTY_RATIO = 0.004; // <0.4% dark pixels → empty row
    const MIN_GAP     = 3;     // need 3+ consecutive empty rows to end a line

    let lineStart  = -1;
    let lineEnd    = -1;
    let inLine     = false;
    let emptyCount = 0;
    let emptyStart = -1;

    // Skip first 5% — avoids accidental QR remnants at very top
    const skip = Math.floor(h * 0.05);

    for (let y = skip; y < h; y++) {
        let dark = 0;
        for (let x = 0; x < w; x++) {
            if (data[(y * w + x) * 4] < 128) dark++;
        }
        const ratio = dark / w;

        if (!inLine) {
            if (ratio >= TEXT_RATIO) {
                inLine     = true;
                lineStart  = Math.max(0, y - 4);
                emptyCount = 0;
            }
        } else {
            if (ratio < EMPTY_RATIO) {
                if (emptyCount === 0) emptyStart = y;
                emptyCount++;
                if (emptyCount >= MIN_GAP) {
                    // Cut at the actual start of the gap (+2px padding)
                    lineEnd = Math.min(h, emptyStart + 2);
                    break;
                }
            } else {
                emptyCount = 0;
                emptyStart = -1;
            }
        }
    }

    // No clean gap found → take 50% of region from where text started
    if (lineStart >= 0 && lineEnd < 0) {
        lineEnd = Math.min(h, lineStart + Math.max(20, Math.floor((h - lineStart) * 0.50)));
    }
    if (lineStart < 0 || lineEnd <= lineStart) return null;

    const lineH = lineEnd - lineStart;
    const out = document.createElement('canvas');
    out.width  = w;
    out.height = lineH;
    out.getContext('2d').drawImage(binaryCanvas, 0, lineStart, w, lineH, 0, 0, w, lineH);
    return out;
}

/** Return a canvas containing only the top `fraction` of the source. */
function cropTopPortion(src, fraction) {
    const h = Math.max(10, Math.floor(src.height * fraction));
    const out = document.createElement('canvas');
    out.width  = src.width;
    out.height = h;
    out.getContext('2d').drawImage(src, 0, 0, src.width, h, 0, 0, src.width, h);
    return out;
}

/** Add white padding and scale up 5× for Tesseract accuracy. */
function scaleCanvasForOcr(src) {
    const pad = 30;
    const padded = document.createElement('canvas');
    padded.width  = src.width  + pad * 2;
    padded.height = src.height + pad * 2;
    const pctx = padded.getContext('2d');
    pctx.fillStyle = '#ffffff';
    pctx.fillRect(0, 0, padded.width, padded.height);
    pctx.drawImage(src, pad, pad);

    const scale = 5;
    const out = document.createElement('canvas');
    out.width  = padded.width  * scale;
    out.height = padded.height * scale;
    const octx = out.getContext('2d');
    octx.imageSmoothingEnabled = false;
    octx.drawImage(padded, 0, 0, out.width, out.height);
    return out;
}

function binarizeCanvas(ctx, w, h, invert) {
    const imgData = ctx.getImageData(0, 0, w, h);
    for (let i = 0; i < imgData.data.length; i += 4) {
        const gray = 0.299 * imgData.data[i] + 0.587 * imgData.data[i + 1] + 0.114 * imgData.data[i + 2];
        const val  = invert ? (gray < 128 ? 255 : 0) : (gray < 128 ? 0 : 255);
        imgData.data[i] = imgData.data[i + 1] = imgData.data[i + 2] = val;
    }
    ctx.putImageData(imgData, 0, 0);
}

async function getArabicOcrWorker(language) {
    if (shamCashArabicWorkerPromise) {
        return shamCashArabicWorkerPromise;
    }

    shamCashArabicWorkerPromise = (async function () {
        const worker = await Tesseract.createWorker({
            logger: () => {}
        });
        await worker.load();
        await worker.loadLanguage(language);
        await worker.initialize(language);
        return worker;
    })();

    return shamCashArabicWorkerPromise;
}

function applyDetectedValues(name, serial) {
    const filled = [];

    if (name) {
        const nameField = document.querySelector('input[name="ShamCashAccountName"]');
        if (nameField) {
            nameField.value = name;
            filled.push('الاسم: ' + name);
        }
    }

    if (serial) {
        const serialField = document.querySelector('input[name="ShamCashAccountNumber"]');
        if (serialField) {
            serialField.value = serial;
            filled.push('الرقم: ' + serial.substring(0, 8) + '...');
        }
    }

    document.getElementById('shamQrStatus').style.display = 'none';
    if (filled.length > 0) {
        document.getElementById('shamQrResultText').textContent = ' ' + filled.join(' | ');
        document.getElementById('shamQrResult').style.display = 'block';
    } else {
        showQrError('لم يتمكن النظام من استخراج البيانات تلقائياً، يرجى إدخالها يدوياً.');
    }
}

function extractBestArabicName(text) {
    const candidates = text
        .split('\n')
        .map(line => sanitizeArabicCandidate(line))
        .filter(Boolean);

    const shortCandidates = candidates.filter(candidate => candidate.replace(/\s/g, '').length <= 14);
    if (shortCandidates.length > 0) {
        candidates.splice(0, candidates.length, ...shortCandidates);
    }

    let best = '';
    let bestScore = -1;
    for (const candidate of candidates) {
        const score = scoreArabicCandidate(candidate);
        if (score > bestScore) {
            best = candidate;
            bestScore = score;
        }
    }

    return best;
}

function sanitizeArabicCandidate(value) {
    if (!value) return '';

    const normalized = (typeof value.normalize === 'function' ? value.normalize('NFKC') : value)
        .replace(/[\u200e\u200f\u202a-\u202e]/g, '')
        .replace(/[\u0640]/g, '')
        .replace(/[#@]/g, ' ')
        .replace(/[A-Za-z0-9]/g, ' ')
        .replace(/[^\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF\s]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();

    const arabicOnly = keepArabicWordsOnly(normalized);
    const cleaned = dropNoisyArabicTokens(stitchArabicTokens(arabicOnly));
    const arabicChars = cleaned.match(/[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/g) || [];
    if (arabicChars.length < 2) return '';

    return normalizeDetectedName(cleaned);
}

function normalizeDetectedName(value) {
    if (!value) return '';
    return (typeof value.normalize === 'function' ? value.normalize('NFKC') : value)
        .replace(/[\u200e\u200f\u202a-\u202e]/g, '')
        .replace(/[\u0640]/g, '')
        .replace(/^[@\s]+/, '')
        .replace(/^@+/, '')
        .replace(/\s+/g, ' ')
        .trim();
}

function stitchArabicTokens(value) {
    const tokens = value.split(/\s+/).filter(Boolean);
    const stitched = [];

    for (let i = 0; i < tokens.length; i++) {
        let token = tokens[i];
        if (token.length <= 1) {
            while (i + 1 < tokens.length && tokens[i + 1].length <= 2) {
                token += tokens[i + 1];
                i++;
            }
        } else if (i + 1 < tokens.length && tokens[i + 1].length === 1 && token.length <= 2) {
            token += tokens[i + 1];
            i++;
        }

        stitched.push(token);
    }

    return stitched.join(' ').replace(/\s+/g, ' ').trim();
}

function dropNoisyArabicTokens(value) {
    const tokens = value.split(/\s+/).filter(Boolean);
    if (tokens.length <= 1) return value;

    const filtered = tokens.filter(token => token.length > 1 && !/(.)\1\1/.test(token));
    return filtered.length > 0 ? filtered.join(' ') : value;
}

function keepArabicWordsOnly(value) {
    const matches = value.match(/[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]+/g);
    return matches ? matches.join(' ') : '';
}

function scoreArabicCandidate(value) {
    const arabicLetters = (value.match(/[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/g) || []).length;
    const words = value.split(/\s+/).filter(Boolean).length;
    const compactLength = value.replace(/\s/g, '').length;
    const compactnessBonus = compactLength <= 14 ? 8 : 0;
    const naturalWordBonus = words >= 1 && words <= 3 ? 10 : 0;
    const repeatedPenalty = hasHeavyRepeatedLetters(value) ? 12 : 0;
    const penalty = compactLength > 18 ? (compactLength - 18) * 3 : 0;
    return arabicLetters * 4 + words * 2 + compactnessBonus + naturalWordBonus - penalty - repeatedPenalty;
}

function hasHeavyRepeatedLetters(value) {
    const compact = value.replace(/\s/g, '');
    return /(.)\1\1/.test(compact);
}

function scoreArabicText(text) {
    const arabicLetters = (text.match(/[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/g) || []).length;
    const latinLetters = (text.match(/[A-Za-z]/g) || []).length;
    const digits = (text.match(/\d/g) || []).length;
    return arabicLetters * 5 - latinLetters * 3 - digits;
}

function drawImageToCanvas(img) {
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    canvas.getContext('2d').drawImage(img, 0, 0);
    return canvas;
}

function setQrStatus(msg) {
    const el = document.getElementById('shamQrStatusText');
    if (el) el.textContent = msg;
}

function showQrDebug(previewCanvas, rawText) {
    const wrapper = document.getElementById('shamQrDebug');
    const preview = document.getElementById('shamQrDebugPreview');
    const text = document.getElementById('shamQrDebugText');
    if (!wrapper || !preview || !text) return;

    wrapper.style.display = 'block';
    text.textContent = rawText || '(فارغ)';

    if (previewCanvas && typeof previewCanvas.toDataURL === 'function') {
        preview.src = previewCanvas.toDataURL('image/png');
        preview.style.display = 'block';
    } else {
        preview.removeAttribute('src');
        preview.style.display = 'none';
    }
}

function hideQrDebug() {
    const wrapper = document.getElementById('shamQrDebug');
    const preview = document.getElementById('shamQrDebugPreview');
    const text = document.getElementById('shamQrDebugText');
    if (wrapper) wrapper.style.display = 'none';
    if (preview) {
        preview.removeAttribute('src');
        preview.style.display = 'none';
    }
    if (text) text.textContent = '';
}

function showQrError(msg) {
    document.getElementById('shamQrErrorText').textContent = msg;
    document.getElementById('shamQrError').style.display = 'block';
}

function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = e => resolve(e.target.result);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function loadImageFromUrl(src) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve(img);
        img.onerror = reject;
        img.src = src;
    });
}

function loadExternalScript(url) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector('script[data-src="' + url + '"]');
        if (existing) {
            if (existing.dataset.loaded === 'true') {
                resolve();
                return;
            }

            existing.addEventListener('load', () => resolve(), { once: true });
            existing.addEventListener('error', reject, { once: true });
            return;
        }

        const s = document.createElement('script');
        s.src = url;
        s.async = true;
        s.dataset.src = url;
        s.onload = () => {
            s.dataset.loaded = 'true';
            resolve();
        };
        s.onerror = reject;
        document.head.appendChild(s);
    });
}
