/* ShamCash QR Image Analyzer — client-side extraction of account name & serial */

async function analyzeShamQr(input) {
    const file = input.files[0];
    if (!file) return;

    document.getElementById('shamQrLabelText').textContent = file.name;
    const statusEl = document.getElementById('shamQrStatus');
    const resultEl = document.getElementById('shamQrResult');
    const errorEl  = document.getElementById('shamQrError');
    statusEl.style.display = 'flex';
    resultEl.style.display = 'none';
    errorEl.style.display  = 'none';
    setQrStatus('جار تحليل الصورة...');

    try {
        const dataUrl = await readFileAsDataUrl(file);
        const img     = await loadImageFromUrl(dataUrl);

        const canvas = document.createElement('canvas');
        canvas.width  = img.naturalWidth;
        canvas.height = img.naturalHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0);

        // ── Step 1: Decode QR code → serial number ────────────────────────
        let serial = '';
        let nameFromQr = '';
        const fullImgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const qr = jsQR(fullImgData.data, fullImgData.width, fullImgData.height, { inversionAttempts: 'attemptBoth' });
        if (qr && qr.data) {
            // Try to extract name from URL params if QR is a URL
            try {
                const url = new URL(qr.data);
                const nameParam = url.searchParams.get('name') || url.searchParams.get('username');
                if (nameParam) nameFromQr = decodeURIComponent(nameParam);
            } catch (e) { /* not a URL */ }
            // Extract hex serial (24+ chars to avoid short matches)
            const hexMatch = qr.data.match(/[a-f0-9]{24,}/i);
            if (hexMatch) {
                serial = hexMatch[0];
            } else {
                // Fallback: strip everything before last / or =
                serial = qr.data.replace(/^.*[/=]/, '').trim();
            }
        }

        // ── Step 2: Find exact dark footer by pixel scanning ──────────────
        setQrStatus('تحديد منطقة النص...');
        const footerTop = findDarkFooterTop(ctx, canvas.width, canvas.height);
        const footerH   = canvas.height - footerTop;

        if (footerH < 20) {
            const fb = Math.floor(canvas.height * 0.18);
            await processFooter(ctx, canvas, canvas.height - fb, fb, serial, nameFromQr);
        } else {
            await processFooter(ctx, canvas, footerTop, footerH, serial, nameFromQr);
        }

    } catch (err) {
        document.getElementById('shamQrStatus').style.display = 'none';
        showQrError('حدث خطأ أثناء التحليل: ' + err.message);
    }
}

/**
 * Scan image rows from the bottom upward.
 * Returns the Y coordinate where the dark footer band starts.
 */
function findDarkFooterTop(ctx, w, h) {
    const sampleX = Math.floor(w / 2);
    let inDark = false;
    for (let y = h - 2; y >= Math.floor(h * 0.55); y--) {
        const px = ctx.getImageData(sampleX, y, 1, 1).data;
        const brightness = (px[0] + px[1] + px[2]) / 3;
        if (!inDark && brightness < 80)  inDark = true;
        if (inDark  && brightness > 160) return y + 1; // transition to footer
    }
    return Math.floor(h * 0.82); // fallback
}

async function processFooter(ctx, canvas, footerTop, footerH, serial, nameFromQr) {
    // Crop exactly the dark footer band
    const fc = document.createElement('canvas');
    fc.width  = canvas.width;
    fc.height = footerH;
    const fctx = fc.getContext('2d');
    fctx.drawImage(canvas, 0, footerTop, canvas.width, footerH, 0, 0, canvas.width, footerH);

    // Binarize: white text on dark → black text on white
    binarizeCanvas(fctx, fc.width, fc.height, true);

    // Add white padding so Tesseract doesn't cut edge characters
    const pad = 20;
    const padded = document.createElement('canvas');
    padded.width  = fc.width  + pad * 2;
    padded.height = fc.height + pad * 2;
    const pctx = padded.getContext('2d');
    pctx.fillStyle = '#ffffff';
    pctx.fillRect(0, 0, padded.width, padded.height);
    pctx.drawImage(fc, pad, pad);

    // Scale up 5x for better OCR accuracy
    const scale = 5;
    const ocrCanvas = document.createElement('canvas');
    ocrCanvas.width  = padded.width  * scale;
    ocrCanvas.height = padded.height * scale;
    const octx = ocrCanvas.getContext('2d');
    octx.imageSmoothingEnabled = false;
    octx.drawImage(padded, 0, 0, ocrCanvas.width, ocrCanvas.height);

    // Lazy-load Tesseract.js
    if (!window.Tesseract) {
        setQrStatus('تحميل محرك القراءة...');
        await loadExternalScript('https://cdn.jsdelivr.net/npm/tesseract.js@4/dist/tesseract.min.js');
    }

    setQrStatus('قراءة النص العربي...');
    const { data: { text } } = await Tesseract.recognize(ocrCanvas, 'ara+eng', {
        logger: () => {},
        tessedit_pageseg_mode: '6'
    });

    let name = nameFromQr;

    // Parse OCR lines
    const lines = text.split('\n').map(l => l.trim()).filter(l => l.length > 1);
    for (const line of lines) {
        if (!serial) {
            const h = line.match(/[a-f0-9]{24,}/i);
            if (h) { serial = h[0]; continue; }
        }
        if (!name) {
            if (/^[a-f0-9]{10,}$/i.test(line)) continue; // skip hex lines
            // Remove leading @ sign (charcode 64)
            name = (line.charCodeAt(0) === 64) ? line.slice(1).trim() : line.trim();
            if (name.length < 2) name = '';
        }
    }

    // Fill the form fields
    const filled = [];
    if (name) {
        const nameField = document.querySelector('input[name="ShamCashAccountName"]');
        if (nameField) { nameField.value = name; filled.push('الاسم: ' + name); }
    }
    if (serial) {
        const serialField = document.querySelector('input[name="ShamCashAccountNumber"]');
        if (serialField) { serialField.value = serial; filled.push('الرقم: ' + serial.substring(0, 8) + '...'); }
    }

    document.getElementById('shamQrStatus').style.display = 'none';
    if (filled.length > 0) {
        document.getElementById('shamQrResultText').textContent = ' ' + filled.join(' | ');
        document.getElementById('shamQrResult').style.display = 'block';
    } else {
        showQrError('لم يتمكن النظام من استخراج البيانات تلقائياً — يرجى إدخالها يدوياً.');
    }
}

/** Convert canvas to pure black/white. invert=true flips dark↔light. */
function binarizeCanvas(ctx, w, h, invert) {
    const imgData = ctx.getImageData(0, 0, w, h);
    for (let i = 0; i < imgData.data.length; i += 4) {
        const gray = 0.299 * imgData.data[i] + 0.587 * imgData.data[i + 1] + 0.114 * imgData.data[i + 2];
        const val  = invert ? (gray < 128 ? 255 : 0) : (gray < 128 ? 0 : 255);
        imgData.data[i] = imgData.data[i + 1] = imgData.data[i + 2] = val;
    }
    ctx.putImageData(imgData, 0, 0);
}

function setQrStatus(msg) {
    const el = document.getElementById('shamQrStatusText');
    if (el) el.textContent = msg;
}

function showQrError(msg) {
    document.getElementById('shamQrErrorText').textContent = msg;
    document.getElementById('shamQrError').style.display = 'block';
}

function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload  = e => resolve(e.target.result);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function loadImageFromUrl(src) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.onload  = () => resolve(img);
        img.onerror = reject;
        img.src = src;
    });
}

function loadExternalScript(url) {
    return new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = url; s.onload = resolve; s.onerror = reject;
        document.head.appendChild(s);
    });
}
