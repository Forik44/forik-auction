// Колесо аукциона: рисование на canvas + синхронизация через SignalR + звук/конфетти.
(function () {
  const cx = 210, cy = 210, R = 200;
  let connection = null;
  let dotnet = null;
  let lastSegments = [];

  function d2r(d) { return (d - 90) * Math.PI / 180; } // 0град = верх (указатель), по часовой
  function ctx() {
    const c = document.getElementById('wheelCanvas');
    return c ? c.getContext('2d') : null;
  }

  // Пустое колесо — когда нет ставок или их меньше двух.
  function drawEmpty(message) {
    const g = ctx(); if (!g) return;
    g.clearRect(0, 0, 420, 420);
    g.save();
    g.beginPath();
    g.setLineDash([10, 10]);
    g.lineWidth = 3;
    g.strokeStyle = '#3a4356';
    g.arc(cx, cy, R, 0, Math.PI * 2);
    g.stroke();
    g.setLineDash([]);
    // центр
    g.beginPath(); g.arc(cx, cy, 34, 0, Math.PI * 2); g.fillStyle = '#0f1320'; g.fill();
    g.lineWidth = 3; g.strokeStyle = '#334155'; g.stroke();
    // текст
    g.fillStyle = '#8b97ad';
    g.font = '600 16px system-ui'; g.textAlign = 'center';
    g.fillText('🎬', cx, cy - 6);
    g.font = '14px system-ui';
    wrapText(g, message || 'Пока пусто — добавьте аниме и ставки', cx, cy + 60, 280, 18);
    g.restore();
  }

  function wrapText(g, text, x, y, maxWidth, lh) {
    const words = text.split(' ');
    let line = '', yy = y;
    for (const w of words) {
      const test = line ? line + ' ' + w : w;
      if (g.measureText(test).width > maxWidth && line) { g.fillText(line, x, yy); line = w; yy += lh; }
      else line = test;
    }
    g.fillText(line, x, yy);
  }

  // segs: [{id,label,weight,color}], rotation в градусах, highlightId — подсветить, dimSet — затемнить
  function draw(segs, rotation, highlightId, dimSet) {
    const g = ctx(); if (!g) return;
    if (!segs || segs.length === 0) { drawEmpty(); return; }
    g.clearRect(0, 0, 420, 420);
    const total = segs.reduce((s, x) => s + x.weight, 0) || 1;
    let start = 0;
    for (const s of segs) {
      const sweep = s.weight / total * 360;
      const a0 = d2r(start + rotation), a1 = d2r(start + sweep + rotation);
      g.beginPath(); g.moveTo(cx, cy); g.arc(cx, cy, R, a0, a1); g.closePath();
      const dim = dimSet && dimSet.has(s.id);
      g.fillStyle = dim ? '#2b3140' : s.color;
      g.globalAlpha = dim ? 0.35 : 1;
      g.fill();
      if (s.id === highlightId) { g.lineWidth = 6; g.strokeStyle = '#fff'; g.stroke(); }
      g.globalAlpha = 1;
      // радиальная подпись: вдоль радиуса, от центра к краю
      const a = d2r(start + sweep / 2 + rotation);
      g.save();
      g.translate(cx, cy);
      g.rotate(a);
      const flip = Math.cos(a) < 0;            // левая половина — переворачиваем, чтобы читалось
      if (flip) g.rotate(Math.PI);
      g.fillStyle = '#fff'; g.font = 'bold 13px system-ui';
      g.textBaseline = 'middle';
      g.textAlign = flip ? 'left' : 'right';
      const label = sweep < 9 ? '' : (s.label.length > 18 ? s.label.slice(0, 17) + '…' : s.label);
      g.fillText(label, flip ? -(R - 16) : (R - 16), 0);
      g.restore();
      start += sweep;
    }
    g.beginPath(); g.arc(cx, cy, 32, 0, Math.PI * 2); g.fillStyle = '#0f1320'; g.fill();
    g.lineWidth = 3; g.strokeStyle = '#334155'; g.stroke();
  }

  function easeOutCubic(t) { return 1 - Math.pow(1 - t, 3); }

  function animateTo(segs, fromAngle, toAngle, ms) {
    return new Promise(resolve => {
      const t0 = performance.now();
      function frame(now) {
        const t = Math.min(1, (now - t0) / ms);
        const a = fromAngle + (toAngle - fromAngle) * easeOutCubic(t);
        draw(segs, a, null, null);
        if (t < 1) requestAnimationFrame(frame); else resolve(a % 360);
      }
      requestAnimationFrame(frame);
    });
  }

  // ---- Победный звук (Web Audio, без файлов) ----
  function playFanfare() {
    try {
      const AC = window.AudioContext || window.webkitAudioContext;
      const ac = new AC();
      const notes = [523.25, 659.25, 783.99, 1046.5]; // C5 E5 G5 C6
      let t = ac.currentTime;
      for (const f of notes) {
        const o = ac.createOscillator(), g = ac.createGain();
        o.type = 'triangle'; o.frequency.value = f;
        o.connect(g); g.connect(ac.destination);
        g.gain.setValueAtTime(0.0001, t);
        g.gain.exponentialRampToValueAtTime(0.25, t + 0.03);
        g.gain.exponentialRampToValueAtTime(0.0001, t + 0.22);
        o.start(t); o.stop(t + 0.24);
        t += 0.16;
      }
    } catch (e) { /* звук не критичен */ }
  }

  // ---- Конфетти (свой мини-движок, без внешних библиотек) ----
  function launchConfetti() {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none;z-index:9999';
    canvas.width = window.innerWidth; canvas.height = window.innerHeight;
    document.body.appendChild(canvas);
    const g = canvas.getContext('2d');
    const colors = ['#ef4444','#f59e0b','#10b981','#3b82f6','#8b5cf6','#ec4899','#eab308'];
    const N = 160;
    const parts = [];
    for (let i = 0; i < N; i++) {
      parts.push({
        x: canvas.width / 2 + (Math.random() - 0.5) * 200,
        y: canvas.height / 3,
        vx: (Math.random() - 0.5) * 12,
        vy: Math.random() * -12 - 4,
        size: 4 + Math.random() * 6,
        color: colors[(Math.random() * colors.length) | 0],
        rot: Math.random() * Math.PI, vr: (Math.random() - 0.5) * 0.3,
      });
    }
    const start = performance.now();
    function frame(now) {
      const elapsed = now - start;
      g.clearRect(0, 0, canvas.width, canvas.height);
      for (const p of parts) {
        p.vy += 0.3; p.x += p.vx; p.y += p.vy; p.rot += p.vr;
        g.save(); g.translate(p.x, p.y); g.rotate(p.rot);
        g.fillStyle = p.color; g.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
        g.restore();
      }
      if (elapsed < 2600) requestAnimationFrame(frame);
      else canvas.remove();
    }
    requestAnimationFrame(frame);
  }

  function celebrate() { playFanfare(); launchConfetti(); }

  async function runPlan(plan) {
    const byId = {};
    for (const s of plan.segments) byId[s.entryId] = { id: s.entryId, label: s.anime, weight: s.weight, color: s.color };
    if (dotnet) await dotnet.invokeMethodAsync('OnSpinStarted');

    for (const step of plan.steps) {
      const segs = step.remainingBefore.map(id => byId[id]);
      await animateTo(segs, 0, step.finalAngleDeg, plan.spinSeconds * 1000);
      const dim = new Set([step.eliminatedEntryId]);
      draw(segs, step.finalAngleDeg % 360, step.eliminatedEntryId, dim);
      await new Promise(r => setTimeout(r, 700));
    }

    const winner = byId[plan.winnerEntryId];
    draw([winner], 0, winner.id, null);
    celebrate();

    const winnerText = `${plan.winnerAnime} (${plan.winnerOwner})`;
    if (dotnet) await dotnet.invokeMethodAsync('OnSpinFinished', winnerText);
  }

  window.wheelInterop = {
    async init(roomId, dotnetRef) {
      dotnet = dotnetRef;
      connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/auction').withAutomaticReconnect().build();

      connection.on('DataChanged', () => { if (dotnet) dotnet.invokeMethodAsync('OnDataChanged'); });
      connection.on('SpinStarted', (plan) => { runPlan(plan); });
      connection.on('SpinBlocked', (msg) => { if (dotnet) dotnet.invokeMethodAsync('OnSpinBlocked', msg); });

      await connection.start();
      await connection.invoke('JoinRoom', roomId);
    },
    drawStatic(segs) {
      lastSegments = segs || [];
      if (lastSegments.length) draw(lastSegments, 0, null, null);
      else drawEmpty();
    },
    async startSpin(roomId, auctionId, seconds) {
      if (connection) await connection.invoke('StartSpin', roomId, auctionId, seconds);
    },
    async notifyChanged(roomId) {
      if (connection) await connection.invoke('NotifyChanged', roomId);
    },
    async dispose(roomId) {
      try { if (connection) { await connection.invoke('LeaveRoom', roomId); await connection.stop(); } } catch (e) {}
      connection = null; dotnet = null;
    }
  };
})();
