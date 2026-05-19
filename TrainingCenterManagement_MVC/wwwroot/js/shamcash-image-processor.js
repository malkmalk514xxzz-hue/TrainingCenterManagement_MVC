/* ShamCash image processing helpers powered by OpenCV.js */

(function () {
    const OPEN_CV_URL = 'https://docs.opencv.org/4.x/opencv.js';
    let openCvReadyPromise = null;

    window.ShamCashImageProcessor = {
        ensureOpenCvReady,
        detectFooterRegion,
        extractNameLineCanvas,
        buildNameOcrVariants,
        buildOcrVariants
    };

    function ensureOpenCvReady() {
        if (window.cv && typeof window.cv.Mat === 'function') {
            return Promise.resolve();
        }

        if (openCvReadyPromise) {
            return openCvReadyPromise;
        }

        openCvReadyPromise = new Promise((resolve, reject) => {
            const existing = document.querySelector('script[data-opencv-script="true"]');
            if (existing && window.cv && typeof window.cv.Mat === 'function') {
                resolve();
                return;
            }

            const previousCallback = window.Module && window.Module.onRuntimeInitialized;
            window.Module = window.Module || {};
            window.Module.onRuntimeInitialized = function () {
                if (typeof previousCallback === 'function') previousCallback();
                resolve();
            };

            if (existing) {
                existing.addEventListener('error', reject, { once: true });
                return;
            }

            const script = document.createElement('script');
            script.src = OPEN_CV_URL;
            script.async = true;
            script.dataset.opencvScript = 'true';
            script.onerror = reject;
            document.head.appendChild(script);
        });

        return openCvReadyPromise;
    }

    function detectFooterRegion(sourceCanvas) {
        const src = cv.imread(sourceCanvas);
        const gray = new cv.Mat();
        cv.cvtColor(src, gray, cv.COLOR_RGBA2GRAY);

        const startRow = Math.floor(gray.rows * 0.55);
        const footerTop = findFooterTop(gray, startRow);
        const footerHeight = Math.max(20, gray.rows - footerTop);
        const footerRect = new cv.Rect(0, footerTop, gray.cols, footerHeight);
        const footerRoi = gray.roi(footerRect);

        const footerCanvas = matToCanvas(footerRoi);

        footerRoi.delete();
        gray.delete();
        src.delete();

        return footerCanvas;
    }

    function buildOcrVariants(footerCanvas) {
        const src = cv.imread(footerCanvas);
        const gray = new cv.Mat();
        cv.cvtColor(src, gray, cv.COLOR_RGBA2GRAY);

        const variants = [];
        variants.push(buildAdaptiveVariant(gray, 31, 12, 5, 24, ['7', '6']));
        variants.push(buildAdaptiveVariant(gray, 41, 8, 5, 24, ['6', '11']));
        variants.push(buildEnhancedGrayVariant(gray, 4, 20, ['7', '11']));
        variants.push(buildBottomHalfVariant(gray, 5, 24, ['7', '11']));

        gray.delete();
        src.delete();
        return variants;
    }

    function buildNameOcrVariants(footerCanvas) {
        const detectedNameLine = extractNameLineCanvas(footerCanvas);
        const src = cv.imread(footerCanvas);
        const gray = new cv.Mat();
        cv.cvtColor(src, gray, cv.COLOR_RGBA2GRAY);

        const variants = [];
        if (detectedNameLine) {
            variants.push({
                canvas: prepareCanvasForOcr(detectedNameLine, 10, 36),
                pageSegModes: ['7', '13']
            });
        }
        variants.push(buildNameBandVariant(gray, 0.00, 0.26, 0.34, 0.66, 12, 44, ['7', '8', '13']));
        variants.push(buildNameBandVariant(gray, 0.02, 0.30, 0.28, 0.72, 10, 38, ['7', '8']));
        variants.push(buildNameBandVariant(gray, 0.00, 0.36, 0.26, 0.74, 8, 30, ['7', '13']));
        variants.push(buildNameBandVariant(gray, 0.04, 0.42, 0.22, 0.78, 8, 30, ['7', '6']));
        variants.push(buildNameBandVariant(gray, 0.08, 0.50, 0.30, 0.70, 9, 32, ['7']));
        variants.push(buildNameGrayVariant(gray, 0.02, 0.40, 0.28, 0.72, 7, 28, ['7']));

        gray.delete();
        src.delete();
        return variants;
    }

    function extractNameLineCanvas(footerCanvas) {
        const src = cv.imread(footerCanvas);
        const gray = new cv.Mat();
        cv.cvtColor(src, gray, cv.COLOR_RGBA2GRAY);

        const xStart = Math.max(0, Math.floor(gray.cols * 0.26));
        const xEnd = Math.min(gray.cols, Math.floor(gray.cols * 0.74));
        const width = Math.max(1, xEnd - xStart);
        const minInkRatio = 0.018;
        const bands = [];
        let bandStart = -1;

        const searchEnd = Math.max(1, Math.floor(gray.rows * 0.42));
        for (let y = 0; y < searchEnd; y++) {
            let brightPixels = 0;
            for (let x = xStart; x < xEnd; x++) {
                if (gray.ucharPtr(y, x)[0] > 150) brightPixels++;
            }

            const ratio = brightPixels / width;
            if (ratio >= minInkRatio) {
                if (bandStart < 0) bandStart = y;
            } else if (bandStart >= 0) {
                const bandHeight = y - bandStart;
                if (bandHeight >= 6) {
                    bands.push({ start: bandStart, end: y - 1, height: bandHeight });
                }
                bandStart = -1;
            }
        }

        if (bandStart >= 0) {
            const bandHeight = searchEnd - bandStart;
            if (bandHeight >= 6) {
                bands.push({ start: bandStart, end: searchEnd - 1, height: bandHeight });
            }
        }

        gray.delete();
        src.delete();

        if (bands.length === 0) return null;

        const mergedBands = mergeNearbyBands(bands, 10);
        const nameBand = mergedBands[0];
        const cropY = Math.max(0, nameBand.start - 5);
        const cropBottom = Math.min(footerCanvas.height, nameBand.end + 5);
        const cropHeight = Math.max(1, cropBottom - cropY);
        return cropCanvasDom(footerCanvas, xStart, cropY, width, cropHeight);
    }

    function mergeNearbyBands(bands, maxGap) {
        if (bands.length === 0) return [];
        const merged = [bands[0]];

        for (let i = 1; i < bands.length; i++) {
            const current = bands[i];
            const last = merged[merged.length - 1];
            if (current.start - last.end <= maxGap) {
                last.end = current.end;
                last.height = last.end - last.start + 1;
            } else {
                merged.push({ start: current.start, end: current.end, height: current.height });
            }
        }

        return merged;
    }

    function findFooterTop(gray, startRow) {
        const x1 = Math.floor(gray.cols * 0.15);
        const x2 = Math.floor(gray.cols * 0.85);
        const bandWidth = Math.max(1, x2 - x1);
        let inDarkBand = false;
        let lastDarkRow = Math.floor(gray.rows * 0.82);

        for (let y = gray.rows - 2; y >= startRow; y--) {
            let darkPixels = 0;
            for (let x = x1; x < x2; x++) {
                if (gray.ucharPtr(y, x)[0] < 105) darkPixels++;
            }

            const darkRatio = darkPixels / bandWidth;
            if (!inDarkBand && darkRatio > 0.35) {
                inDarkBand = true;
                lastDarkRow = y;
            }

            if (inDarkBand && darkRatio < 0.12) {
                return y + 1;
            }
        }

        return lastDarkRow;
    }

    function buildAdaptiveVariant(gray, blockSize, cValue, scale, padding, pageSegModes) {
        const threshold = new cv.Mat();
        const kernel = cv.getStructuringElement(cv.MORPH_RECT, new cv.Size(3, 3));
        const morphed = new cv.Mat();

        cv.adaptiveThreshold(gray, threshold, 255, cv.ADAPTIVE_THRESH_GAUSSIAN_C, cv.THRESH_BINARY_INV, blockSize, cValue);
        cv.morphologyEx(threshold, morphed, cv.MORPH_CLOSE, kernel);

        const bounds = detectInkBounds(morphed);
        const roi = morphed.roi(bounds);
        const inverted = new cv.Mat();
        cv.bitwise_not(roi, inverted);

        const canvas = prepareCanvasForOcr(matToCanvas(inverted), scale, padding);

        inverted.delete();
        roi.delete();
        morphed.delete();
        kernel.delete();
        threshold.delete();

        return { canvas, pageSegModes };
    }

    function buildEnhancedGrayVariant(gray, scale, padding, pageSegModes) {
        const equalized = new cv.Mat();
        const threshold = new cv.Mat();
        cv.equalizeHist(gray, equalized);
        cv.threshold(equalized, threshold, 0, 255, cv.THRESH_BINARY + cv.THRESH_OTSU);

        const bounds = detectInkBounds(threshold);
        const roi = threshold.roi(bounds);
        const canvas = prepareCanvasForOcr(matToCanvas(roi), scale, padding);

        roi.delete();
        threshold.delete();
        equalized.delete();

        return { canvas, pageSegModes };
    }

    function buildBottomHalfVariant(gray, scale, padding, pageSegModes) {
        const startY = Math.max(0, Math.floor(gray.rows * 0.28));
        const rect = new cv.Rect(0, startY, gray.cols, Math.max(1, gray.rows - startY));
        const roi = gray.roi(rect);
        const threshold = new cv.Mat();
        cv.adaptiveThreshold(roi, threshold, 255, cv.ADAPTIVE_THRESH_GAUSSIAN_C, cv.THRESH_BINARY_INV, 31, 10);

        const bounds = detectInkBounds(threshold);
        const textRoi = threshold.roi(bounds);
        const inverted = new cv.Mat();
        cv.bitwise_not(textRoi, inverted);
        const canvas = prepareCanvasForOcr(matToCanvas(inverted), scale, padding);

        inverted.delete();
        textRoi.delete();
        threshold.delete();
        roi.delete();

        return { canvas, pageSegModes };
    }

    function buildNameBandVariant(gray, startRatio, endRatio, startXRatio, endXRatio, scale, padding, pageSegModes) {
        const startY = Math.max(0, Math.floor(gray.rows * startRatio));
        const endY = Math.min(gray.rows, Math.floor(gray.rows * endRatio));
        const startX = Math.max(0, Math.floor(gray.cols * startXRatio));
        const endX = Math.min(gray.cols, Math.floor(gray.cols * endXRatio));
        const rect = new cv.Rect(
            startX,
            startY,
            Math.max(1, endX - startX),
            Math.max(1, endY - startY)
        );
        const roi = gray.roi(rect);
        const threshold = new cv.Mat();
        const kernel = cv.getStructuringElement(cv.MORPH_RECT, new cv.Size(3, 3));
        const morphed = new cv.Mat();

        cv.adaptiveThreshold(roi, threshold, 255, cv.ADAPTIVE_THRESH_GAUSSIAN_C, cv.THRESH_BINARY_INV, 25, 8);
        cv.morphologyEx(threshold, morphed, cv.MORPH_CLOSE, kernel);

        const bounds = detectInkBounds(morphed);
        const textRoi = morphed.roi(bounds);
        const inverted = new cv.Mat();
        cv.bitwise_not(textRoi, inverted);
        const canvas = prepareCanvasForOcr(matToCanvas(inverted), scale, padding);

        inverted.delete();
        textRoi.delete();
        morphed.delete();
        kernel.delete();
        threshold.delete();
        roi.delete();

        return { canvas, pageSegModes };
    }

    function buildNameGrayVariant(gray, startRatio, endRatio, startXRatio, endXRatio, scale, padding, pageSegModes) {
        const startY = Math.max(0, Math.floor(gray.rows * startRatio));
        const endY = Math.min(gray.rows, Math.floor(gray.rows * endRatio));
        const startX = Math.max(0, Math.floor(gray.cols * startXRatio));
        const endX = Math.min(gray.cols, Math.floor(gray.cols * endXRatio));
        const rect = new cv.Rect(
            startX,
            startY,
            Math.max(1, endX - startX),
            Math.max(1, endY - startY)
        );
        const roi = gray.roi(rect);
        const equalized = new cv.Mat();
        const threshold = new cv.Mat();

        cv.equalizeHist(roi, equalized);
        cv.threshold(equalized, threshold, 0, 255, cv.THRESH_BINARY + cv.THRESH_OTSU);

        const canvas = prepareCanvasForOcr(matToCanvas(threshold), scale, padding);

        threshold.delete();
        equalized.delete();
        roi.delete();

        return { canvas, pageSegModes };
    }

    function detectInkBounds(binaryMat) {
        let minX = binaryMat.cols;
        let minY = binaryMat.rows;
        let maxX = -1;
        let maxY = -1;

        for (let y = 0; y < binaryMat.rows; y++) {
            for (let x = 0; x < binaryMat.cols; x++) {
                if (binaryMat.ucharPtr(y, x)[0] > 0) {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0 || maxY < 0) {
            return new cv.Rect(0, 0, binaryMat.cols, binaryMat.rows);
        }

        const rect = new cv.Rect(
            minX,
            minY,
            Math.max(1, maxX - minX + 1),
            Math.max(1, maxY - minY + 1)
        );

        const paddedX = Math.max(0, rect.x - 12);
        const paddedY = Math.max(0, rect.y - 12);
        const paddedWidth = Math.min(binaryMat.cols - paddedX, rect.width + 24);
        const paddedHeight = Math.min(binaryMat.rows - paddedY, rect.height + 24);
        return new cv.Rect(paddedX, paddedY, Math.max(1, paddedWidth), Math.max(1, paddedHeight));
    }

    function matToCanvas(mat) {
        const canvas = document.createElement('canvas');
        canvas.width = mat.cols;
        canvas.height = mat.rows;
        cv.imshow(canvas, mat);
        return canvas;
    }

    function cropCanvasDom(sourceCanvas, x, y, width, height) {
        const cropped = document.createElement('canvas');
        cropped.width = Math.max(1, width);
        cropped.height = Math.max(1, height);
        cropped.getContext('2d').drawImage(
            sourceCanvas,
            x,
            y,
            width,
            height,
            0,
            0,
            cropped.width,
            cropped.height
        );
        return cropped;
    }

    function prepareCanvasForOcr(sourceCanvas, scale, padding) {
        const padded = document.createElement('canvas');
        padded.width = sourceCanvas.width + padding * 2;
        padded.height = sourceCanvas.height + padding * 2;
        const paddedCtx = padded.getContext('2d');
        paddedCtx.fillStyle = '#ffffff';
        paddedCtx.fillRect(0, 0, padded.width, padded.height);
        paddedCtx.drawImage(sourceCanvas, padding, padding);

        const result = document.createElement('canvas');
        result.width = padded.width * scale;
        result.height = padded.height * scale;
        const resultCtx = result.getContext('2d');
        resultCtx.imageSmoothingEnabled = false;
        resultCtx.drawImage(padded, 0, 0, result.width, result.height);
        return result;
    }
})();
