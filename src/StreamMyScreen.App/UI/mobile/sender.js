let signalingSocket = null;
let peerConnection = null;
let localStream = null;
let wakeLock = null;
let timerInterval = null;
let secondsElapsed = 0;

const statusPill = document.getElementById('statusPill');
const statusText = document.getElementById('statusText');
const readySection = document.getElementById('readySection');
const streamingSection = document.getElementById('streamingSection');
const startBtn = document.getElementById('startBtn');
const stopBtn = document.getElementById('stopBtn');
const qualitySelect = document.getElementById('qualitySelect');
const audioToggle = document.getElementById('audioToggle');
const timerDisplay = document.getElementById('timerDisplay');

window.addEventListener('DOMContentLoaded', () => {
  connectSignaling();
  setupEvents();
});

function connectSignaling() {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const wsUrl = `${protocol}//${window.location.host}/ws?role=sender`;

  signalingSocket = new WebSocket(wsUrl);

  signalingSocket.onopen = () => {
    statusPill.classList.add('connected');
    statusText.textContent = 'Connected (Ready)';
  };

  signalingSocket.onmessage = async (event) => {
    const msg = JSON.parse(event.data);
    switch (msg.type) {
      case 'answer':
        if (peerConnection) {
          await peerConnection.setRemoteDescription(new RTCSessionDescription(msg.answer));
        }
        break;
      case 'candidate':
        if (peerConnection) {
          try {
            await peerConnection.addIceCandidate(new RTCIceCandidate(msg.candidate));
          } catch (e) {
            console.warn('ICE error', e);
          }
        }
        break;
      case 'stop-stream':
        stopStreaming();
        break;
    }
  };

  signalingSocket.onclose = () => {
    statusPill.classList.remove('connected');
    statusText.textContent = 'Disconnected...';
    setTimeout(connectSignaling, 2000);
  };
}

function setupEvents() {
  startBtn.addEventListener('click', startStreaming);
  stopBtn.addEventListener('click', stopStreaming);
}

async function startStreaming() {
  if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
    alert("Mobile browsers restrict screen capture on Android.\n\nTo mirror without installing apps:\n1. Swipe down Android notification panel.\n2. Tap 'Smart View' (Samsung) or 'Cast / Screen cast'.\n3. Select your PC to mirror instantly.");
    return;
  }

  const height = parseInt(qualitySelect.value, 10);
  const width = height === 1080 ? 1920 : 1280;
  const includeAudio = audioToggle.checked;

  try {
    localStream = await navigator.mediaDevices.getDisplayMedia({
      video: {
        cursor: 'always',
        frameRate: { ideal: 60, max: 60 },
        width: { ideal: width },
        height: { ideal: height }
      },
      audio: includeAudio
    });

    // Detect if user stops sharing from phone status bar
    localStream.getVideoTracks()[0].onended = () => {
      stopStreaming();
    };

    // Request WakeLock to prevent screen timeout
    if ('wakeLock' in navigator) {
      try {
        wakeLock = await navigator.wakeLock.request('screen');
      } catch (e) {
        console.log('WakeLock not supported or allowed', e);
      }
    }

    peerConnection = new RTCPeerConnection({
      iceServers: []
    });

    localStream.getTracks().forEach(track => {
      peerConnection.addTrack(track, localStream);
    });

    peerConnection.onicecandidate = (event) => {
      if (event.candidate && signalingSocket?.readyState === WebSocket.OPEN) {
        signalingSocket.send(JSON.stringify({
          type: 'candidate',
          candidate: event.candidate
        }));
      }
    };

    const offer = await peerConnection.createOffer();
    await peerConnection.setLocalDescription(offer);

    signalingSocket.send(JSON.stringify({
      type: 'offer',
      offer: offer
    }));

    // Switch View
    readySection.classList.add('hidden');
    streamingSection.classList.remove('hidden');
    startTimer();
  } catch (err) {
    console.error('Failed to start screen capture', err);
    alert("Screen recording not allowed in browser: " + (err.message || err.name) + "\n\nPlease swipe down your notification panel and select Smart View / Cast.");
  }
}

function stopStreaming() {
  stopTimer();

  if (wakeLock) {
    wakeLock.release().catch(() => {});
    wakeLock = null;
  }

  if (localStream) {
    localStream.getTracks().forEach(t => t.stop());
    localStream = null;
  }

  if (peerConnection) {
    peerConnection.close();
    peerConnection = null;
  }

  streamingSection.classList.add('hidden');
  readySection.classList.remove('hidden');
}

function startTimer() {
  secondsElapsed = 0;
  updateTimerUI();
  clearInterval(timerInterval);
  timerInterval = setInterval(() => {
    secondsElapsed++;
    updateTimerUI();
  }, 1000);
}

function stopTimer() {
  clearInterval(timerInterval);
  secondsElapsed = 0;
}

function updateTimerUI() {
  const h = String(Math.floor(secondsElapsed / 3600)).padStart(2, '0');
  const m = String(Math.floor((secondsElapsed % 3600) / 60)).padStart(2, '0');
  const s = String(secondsElapsed % 60).padStart(2, '0');
  timerDisplay.textContent = `${h}:${m}:${s}`;
}

