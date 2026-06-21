// Колесо аукциона: рисование на canvas + синхронизация через SignalR.
(function () {
  const cx = 210, cy = 210, R = 200;
  let connection = null;
  let dotnet = null;
  let initiatedAuctionId = null;
  let lastSegments = [];

  function d2r(d) { return (d - 90) * Math.PI / 180; } // 0град = верх (указатель), по часовой
  function ctx() {
    const c = document.getElementById('wheelCanvas');
    return c ? c.getContext('2d') : null;
  }

  // segs: [{id,label,weight,color}], rotation в градусах, highlightId — подсветить, dimSet — затемнить
  function draw(segs, rotation, highlightId, dimSet) {
    const g = ctx(); if (!g) return;
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
      // подпись
      const mid = d2r(start + sweep / 2 + rotation);
      g.save();
      g.translate(cx + Math.cos(mid) * R * 0.62, cy + Math.sin(mid) * R * 0.62);
      g.rotate(mid + Math.PI / 2);
      g.fillStyle = '#fff'; g.font = 'bold 13px system-ui'; g.textAlign = 'center';
      const label = sweep < 12 ? '' : (s.label.length > 14 ? s.label.slice(0, 13) + '…' : s.label);
      g.fillText(label, 0, 0);
      g.restore();
      start += sweep;
    }
    // центр
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

  async function runPlan(plan) {
    const byId = {};
    for (const s of plan.segments) byId[s.entryId] = { id: s.entryId, label: s.anime, weight: s.weight, color: s.color };
    if (dotnet) await dotnet.invokeMethodAsync('OnSpinStarted');

    for (const step of plan.steps) {
      const segs = step.remainingBefore.map(id => byId[id]);
      await animateTo(segs, 0, step.finalAngleDeg, plan.spinSeconds * 1000);
      // затемнить выбывшего и пауза
      const dim = new Set([step.eliminatedEntryId]);
      draw(segs, step.finalAngleDeg % 360, step.eliminatedEntryId, dim);
      await new Promise(r => setTimeout(r, 700));
    }

    // победитель — одиночный сектор с подсветкой
    const winner = byId[plan.winnerEntryId];
    draw([winner], 0, winner.id, null);

    const winnerText = `${plan.winnerAnime} (${plan.winnerOwner})`;
    if (dotnet) await dotnet.invokeMethodAsync('OnSpinFinished', winnerText);

    // только инициатор фиксирует итог в БД
    if (initiatedAuctionId === plan.auctionId && connection) {
      await connection.invoke('FinishSpin', plan._roomId, plan.auctionId, plan.winnerEntryId);
      initiatedAuctionId = null;
    }
  }

  window.wheelInterop = {
    async init(roomId, dotnetRef) {
      dotnet = dotnetRef;
      connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/auction').withAutomaticReconnect().build();

      connection.on('DataChanged', () => { if (dotnet) dotnet.invokeMethodAsync('OnDataChanged'); });
      connection.on('SpinStarted', (plan) => { plan._roomId = roomId; runPlan(plan); });
      connection.on('AuctionFinished', () => { if (dotnet) dotnet.invokeMethodAsync('OnDataChanged'); });

      await connection.start();
      await connection.invoke('JoinRoom', roomId);
    },
    drawStatic(segs) {
      lastSegments = segs || [];
      if (lastSegments.length) draw(lastSegments, 0, null, null);
      else { const g = ctx(); if (g) g.clearRect(0, 0, 420, 420); }
    },
    async startSpin(roomId, auctionId, seconds) {
      initiatedAuctionId = auctionId;
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
