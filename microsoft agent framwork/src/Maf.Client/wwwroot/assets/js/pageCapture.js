window.pageCapture = (function () {
    /** @typedef {'browser'|'monitor'} CaptureSurface */

    function timestamp() {
        return new Date().toISOString().replace(/[:.]/g, "-");
    }

    function downloadBlob(blob, fileName) {
        const objectUrl = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = objectUrl;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () {
            URL.revokeObjectURL(objectUrl);
        }, 1000);
    }

    function stopStream(stream) {
        if (!stream) {
            return;
        }
        stream.getTracks().forEach(function (track) {
            track.stop();
        });
    }

    function isMac() {
        return /Mac/i.test(navigator.userAgent || "");
    }

    async function waitForVideoFrame(video) {
        video.playsInline = true;
        video.muted = true;
        await video.play();
        if (video.readyState >= 2) {
            if (isMac()) {
                await new Promise(function (resolve) {
                    window.setTimeout(resolve, 600);
                });
            }
            return;
        }
        await new Promise(function (resolve, reject) {
            video.onloadedmetadata = function () {
                resolve();
            };
            video.onerror = function () {
                reject(new Error("Failed to read video frame from capture."));
            };
        });
        if (isMac()) {
            await new Promise(function (resolve) {
                window.setTimeout(resolve, 600);
            });
        }
    }

    async function frameToPngBlob(video) {
        const canvas = document.createElement("canvas");
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext("2d");
        if (!ctx) {
            throw new Error("Canvas is not available.");
        }
        ctx.drawImage(video, 0, 0);
        return new Promise(function (resolve, reject) {
            canvas.toBlob(
                function (blob) {
                    if (blob) {
                        resolve(blob);
                    } else {
                        reject(new Error("Failed to create PNG."));
                    }
                },
                "image/png"
            );
        });
    }

    /**
     * Maps to Chrome share dialog tabs:
     * - browser → "Chrome Tab" (pick a tab)
     * - monitor → "Entire Screen" (full display)
     */
    function getDisplayMediaOptions(surface) {
        if (surface === "browser") {
            return {
                video: { displaySurface: "browser" },
                audio: false,
                preferCurrentTab: true,
                selfBrowserSurface: "include",
                systemAudio: "exclude",
                monitorTypeSurfaces: "exclude",
            };
        }
        if (surface === "monitor") {
            return {
                video: { displaySurface: "monitor" },
                audio: false,
            };
        }
        throw new Error("Unknown capture surface: " + surface);
    }

    function filePrefix(surface) {
        if (surface === "browser") {
            return "erp-screenshot-tab";
        }
        return "erp-screenshot-screen";
    }

    function recordingFilePrefix(surface) {
        if (surface === "browser") {
            return "erp-recording-tab";
        }
        return "erp-recording-screen";
    }

    function pickVideoMimeType() {
        const candidates = [
            "video/webm;codecs=vp9",
            "video/webm;codecs=vp8",
            "video/webm",
        ];
        for (let i = 0; i < candidates.length; i++) {
            if (MediaRecorder.isTypeSupported(candidates[i])) {
                return candidates[i];
            }
        }
        return "";
    }

    let mediaRecorder = null;
    let recordChunks = [];
    let recordStream = null;
    let recordingSurface = null;

    async function captureFromSurface(surface) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
            throw new Error("Screen capture is not supported in this browser.");
        }

        const stream = await navigator.mediaDevices.getDisplayMedia(
            getDisplayMediaOptions(surface)
        );

        const video = document.createElement("video");
        video.srcObject = stream;

        try {
            await waitForVideoFrame(video);
            const blob = await frameToPngBlob(video);
            downloadBlob(blob, filePrefix(surface) + "-" + timestamp() + ".png");
        } finally {
            stopStream(stream);
            video.srcObject = null;
        }
    }

    function blobToBase64(blob) {
        return new Promise(function (resolve, reject) {
            const reader = new FileReader();
            reader.onload = function () {
                const value = String(reader.result || "");
                const split = value.indexOf(",");
                resolve(split >= 0 ? value.slice(split + 1) : value);
            };
            reader.onerror = function () {
                reject(new Error("Failed to encode PNG."));
            };
            reader.readAsDataURL(blob);
        });
    }

    async function captureBrowserBase64() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
            throw new Error("Screen capture is not supported in this browser.");
        }

        const stream = await navigator.mediaDevices.getDisplayMedia(
            getDisplayMediaOptions("browser")
        );

        const video = document.createElement("video");
        video.srcObject = stream;

        try {
            await waitForVideoFrame(video);
            const blob = await frameToPngBlob(video);
            downloadBlob(blob, filePrefix("browser") + "-" + timestamp() + ".png");
            return blobToBase64(blob);
        } finally {
            stopStream(stream);
            video.srcObject = null;
        }
    }

    async function startRecording(surface) {
        if (mediaRecorder) {
            throw new Error("A recording is already in progress. Stop it first.");
        }
        if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
            throw new Error("Screen capture is not supported in this browser.");
        }
        if (typeof MediaRecorder === "undefined") {
            throw new Error("MediaRecorder is not supported in this browser.");
        }

        recordingSurface = surface;
        recordStream = await navigator.mediaDevices.getDisplayMedia(
            getDisplayMediaOptions(surface)
        );
        recordChunks = [];

        const mimeType = pickVideoMimeType();
        mediaRecorder = mimeType
            ? new MediaRecorder(recordStream, { mimeType: mimeType })
            : new MediaRecorder(recordStream);

        mediaRecorder.addEventListener("dataavailable", function (event) {
            if (event.data && event.data.size > 0) {
                recordChunks.push(event.data);
            }
        });

        const stopWhenShareEnds = function () {
            void stopRecordingInternal();
        };
        recordStream.getVideoTracks().forEach(function (track) {
            track.addEventListener("ended", stopWhenShareEnds, { once: true });
        });

        mediaRecorder.start(1000);
    }

    async function stopRecordingInternal() {
        if (!mediaRecorder) {
            return;
        }

        const recorder = mediaRecorder;
        const stream = recordStream;
        const chunks = recordChunks;
        const surface = recordingSurface || "browser";

        const blob = await new Promise(function (resolve) {
            recorder.addEventListener(
                "stop",
                function () {
                    resolve(
                        new Blob(chunks, {
                            type: recorder.mimeType || "video/webm",
                        })
                    );
                },
                { once: true }
            );
            recorder.stop();
        });

        stopStream(stream);
        mediaRecorder = null;
        recordStream = null;
        recordChunks = [];
        recordingSurface = null;

        downloadBlob(blob, recordingFilePrefix(surface) + "-" + timestamp() + ".webm");
    }

    return {
        /** Default: browser tab (same as captureScreenshotBrowser). */
        captureScreenshot: async function (surface) {
            const mode =
                surface === "monitor" || surface === "browser"
                    ? surface
                    : "browser";
            await captureFromSurface(mode);
        },

        captureScreenshotBrowser: function () {
            return captureFromSurface("browser");
        },

        captureScreenshotBrowserBase64: function () {
            return captureBrowserBase64();
        },

        /** Full monitor (Chrome dialog: "Entire Screen"). */
        captureScreenshotScreen: function () {
            return captureFromSurface("monitor");
        },

        isRecording: function () {
            return !!mediaRecorder;
        },

        stopRecording: function () {
            return stopRecordingInternal();
        },

        startRecordingBrowser: function () {
            return startRecording("browser");
        },

        startRecordingScreen: function () {
            return startRecording("monitor");
        },
    };
})();
