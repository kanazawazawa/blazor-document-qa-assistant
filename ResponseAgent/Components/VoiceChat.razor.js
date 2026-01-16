// VoiceChat.razor.js
// Web Audio API を使用した音声録音と再生

let audioContext = null;
let mediaStream = null;
let mediaRecorder = null;
let recordedChunks = [];

export async function startRecording() {
    try {
        // Audio Context を初期化
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        // マイクへのアクセスをリクエスト
        mediaStream = await navigator.mediaDevices.getUserMedia({ 
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        // MediaRecorder を初期化
        const mimeType = 'audio/wav';
        recordedChunks = [];
        
        mediaRecorder = new MediaRecorder(mediaStream, {
            mimeType: mimeType,
            audioBitsPerSecond: 128000
        });

        mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                recordedChunks.push(event.data);
            }
        };

        mediaRecorder.start();
        console.log('Recording started');
    } catch (error) {
        console.error('Recording failed:', error);
        throw new Error(`マイクへのアクセスが拒否されました: ${error.message}`);
    }
}

export async function stopRecording() {
    return new Promise((resolve, reject) => {
        if (!mediaRecorder) {
            reject(new Error('Recording not started'));
            return;
        }

        mediaRecorder.onstop = async () => {
            try {
                // 音声データを Blob にまとめる
                const audioBlob = new Blob(recordedChunks, { type: 'audio/wav' });
                
                // Blob を ArrayBuffer に変換
                const arrayBuffer = await audioBlob.arrayBuffer();
                const uint8Array = new Uint8Array(arrayBuffer);
                
                // ストリームを停止
                if (mediaStream) {
                    mediaStream.getTracks().forEach(track => track.stop());
                }

                console.log(`Recording stopped. Audio data size: ${uint8Array.length} bytes`);
                
                // byte[] として返す
                resolve(Array.from(uint8Array));
            } catch (error) {
                reject(error);
            }
        };

        mediaRecorder.stop();
    });
}

export async function playAudio(audioBase64) {
    try {
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        // Base64 をデコード
        const binaryString = atob(audioBase64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }

        // ArrayBuffer にデコード
        const audioBuffer = await audioContext.decodeAudioData(bytes.buffer);
        
        // BufferSource を作成して再生
        const source = audioContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(audioContext.destination);
        source.start(0);

        console.log('Audio playback started');
    } catch (error) {
        console.error('Audio playback failed:', error);
        throw new Error(`音声再生エラー: ${error.message}`);
    }
}

export function stopAudio() {
    if (audioContext) {
        audioContext.close();
        audioContext = null;
    }
}

export async function getAudioWaveform(audioBase64, canvasElement) {
    try {
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        // Base64 をデコード
        const binaryString = atob(audioBase64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }

        // ArrayBuffer にデコード
        const audioBuffer = await audioContext.decodeAudioData(bytes.buffer);
        
        // キャンバスに波形を描画
        drawWaveform(audioBuffer, canvasElement);
    } catch (error) {
        console.error('Waveform analysis failed:', error);
    }
}

function drawWaveform(audioBuffer, canvasElement) {
    if (!canvasElement) return;

    const canvas = canvasElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const rawData = audioBuffer.getChannelData(0);
    const samples = rawData.length;
    const blockSize = Math.floor(samples / canvas.width);
    const filterData = [];
    let sum = 0;
    let rms = 0;

    for (let i = 0; i < samples; i++) {
        sum += rawData[i] * rawData[i];
        if ((i + 1) % blockSize === 0) {
            rms = Math.sqrt(sum / blockSize);
            filterData.push(rms);
            sum = 0;
        }
    }

    // キャンバスをクリア
    ctx.fillStyle = 'rgb(240, 240, 240)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // 波形を描画
    ctx.strokeStyle = 'rgb(63, 81, 181)';
    ctx.lineWidth = 2;
    ctx.beginPath();

    const sliceWidth = (canvas.width * 1.0) / filterData.length;
    let x = 0;

    for (let i = 0; i < filterData.length; i++) {
        const v = filterData[i] * 200.0;
        const y = canvas.height - (v / 2);

        if (i === 0) {
            ctx.moveTo(x, y);
        } else {
            ctx.lineTo(x, y);
        }

        x += sliceWidth;
    }

    ctx.lineTo(canvas.width, canvas.height / 2);
    ctx.stroke();
}
