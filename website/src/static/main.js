(function () {
  var reduce = false;
  try { reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) { /* older browsers */ }

  // GitHub stars
  var stars = document.getElementById('gh-stars');
  if (stars) {
    fetch('https://api.github.com/repos/holgerleichsenring/agent-smith')
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (data.stargazers_count == null) return;
        var n = data.stargazers_count;
        stars.textContent = n >= 1000 ? (n / 1000).toFixed(1).replace(/\.0$/, '') + 'k' : n;
      })
      .catch(function () { /* the dash stays */ });
  }

  var hero = document.querySelector('.hero');
  var life = document.getElementById('life');
  if (reduce) {
    if (life && life.pauseAnimations) { life.pauseAnimations(); document.getElementById('packets').style.display = 'none'; }
    return;
  }

  // ---------- code rain behind the header, dimmed where the text sits ----------
  var rain = document.getElementById('rain');
  if (hero && rain && rain.getContext) {
    var rc = rain.getContext('2d');
    var GL = 'ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123457789Z:.=*+-<>|';
    var R = null;
    var setupRain = function () {
      var band = hero.querySelector('.band');
      var w = hero.clientWidth, h = band ? band.offsetTop : hero.clientHeight;
      var dpr = Math.min(2, window.devicePixelRatio || 1);
      rain.style.height = h + 'px';
      rain.width = Math.round(w * dpr); rain.height = Math.round(h * dpr);
      rc.setTransform(dpr, 0, 0, dpr, 0, 0);
      var small = w < 760, CW = small ? 8 : 9, CH = small ? 11 : 12;
      var cols = Math.ceil(w / CW), rows = Math.ceil(h / CH);
      var colW = new Float32Array(cols);
      for (var k = 0; k < cols; k++) colW[k] = small ? .35 : .2 + .8 * Math.min(1, Math.max(0, (k / cols - .15) / .45));
      var G = new Uint8Array(cols * rows);
      for (var i = 0; i < G.length; i++) G[i] = (Math.random() * GL.length) | 0;
      var drops = [];
      for (var c = 0; c < cols; c++) drops.push({ y: -Math.random() * rows * .6, v: .35 + Math.random() * .6 });
      R = { w: w, h: h, CW: CW, CH: CH, cols: cols, rows: rows, A: new Float32Array(cols * rows), G: G, drops: drops, colW: colW,
        heads: new Int32Array(cols + 1), gUntil: 0, gNext: performance.now() + 4000, gA: 0, gB: 0, gS: 0 };
    };
    var rainFrame = function (now) {
      var cols = R.cols, rows = R.rows, A = R.A, G = R.G, CW = R.CW, CH = R.CH, n = cols * rows, i, r, q;
      for (i = 0; i < n; i++) A[i] *= .885;
      for (q = 0; q < cols; q++) {
        var d = R.drops[q]; d.y += d.v; r = Math.floor(d.y);
        if (r >= 0 && r < rows) { i = r * cols + q; A[i] = 1; G[i] = (Math.random() * GL.length) | 0; }
        if (d.y > rows + Math.random() * 30) { d.y = -Math.random() * 12; d.v = .35 + Math.random() * .6; }
      }
      for (i = 0; i < n * .015; i++) G[(Math.random() * n) | 0] = (Math.random() * GL.length) | 0;
      if (now > R.gNext) {
        R.gUntil = now + 160; R.gA = (Math.random() * rows) | 0; R.gB = R.gA + 2 + ((Math.random() * 5) | 0);
        R.gS = (Math.random() < .5 ? -1 : 1) * (2 + ((Math.random() * 4) | 0)); R.gNext = now + 3200 + Math.random() * 4500;
      }
      var glitch = now < R.gUntil, nh = 0;
      rc.clearRect(0, 0, R.w, R.h);
      rc.font = '500 ' + (CH - 1) + 'px "IBM Plex Mono", "Hiragino Kaku Gothic ProN", "Meiryo", monospace';
      rc.textBaseline = 'top';
      rc.fillStyle = '#22c55e';
      for (r = 0; r < rows; r++) {
        var shift = glitch && r >= R.gA && r <= R.gB ? R.gS * CW : 0;
        for (q = 0; q < cols; q++) {
          i = r * cols + q;
          var v = A[i] * R.colW[q] * .6;
          if (v < .05) continue;
          if (A[i] > .98) { R.heads[nh++] = i; continue; }
          rc.globalAlpha = v; rc.fillText(GL[G[i]], q * CW + shift, r * CH);
        }
      }
      rc.fillStyle = '#f4fff7';
      for (var k = 0; k < nh; k++) {
        i = R.heads[k]; r = (i / cols) | 0; q = i - r * cols;
        rc.globalAlpha = .5 + .5 * R.colW[q]; rc.fillText(GL[G[i]], q * CW, r * CH);
      }
      rc.globalAlpha = 1;
    };
    setupRain();
    var heroOn = true;
    if ('IntersectionObserver' in window) new IntersectionObserver(function (es) { heroOn = es[0].isIntersecting; }).observe(hero);
    setInterval(function () { if (heroOn && !document.hidden) rainFrame(performance.now()); }, 40);
    var rz;
    window.addEventListener('resize', function () { clearTimeout(rz); rz = setTimeout(setupRain, 150); });

    hero.addEventListener('pointermove', function (e) {
      var b = hero.getBoundingClientRect();
      hero.style.setProperty('--mx', (e.clientX - b.left) + 'px');
      hero.style.setProperty('--my', (e.clientY - b.top) + 'px');
    });
  }

  // ---------- headline: each line decodes as it lands, then a rare glitch ----------
  var h1 = document.querySelector('.hero h1, .page-hero h1');
  if (h1) {
    var DEC = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#%&@$';
    var leaves = Array.prototype.filter.call(h1.querySelectorAll('.w1, .out, .w3'), Boolean);
    var words = leaves.map(function (el) { return el.textContent; });
    var decode = function (el, word, dur) {
      var t0 = performance.now();
      (function step() {
        var k = Math.min(1, (performance.now() - t0) / dur), n = Math.floor(k * word.length), out = word.slice(0, n);
        for (var i = n; i < word.length; i++) out += word[i] === ' ' ? ' ' : DEC[(Math.random() * DEC.length) | 0];
        el.textContent = out;
        if (k < 1) setTimeout(step, 40); else el.textContent = word;
      })();
    };
    leaves.forEach(function (el, i) { setTimeout(function () { decode(el, words[i], i ? 520 : 600); }, i ? 820 + (i - 1) * 140 : 0); });
    setInterval(function () {
      if (document.hidden || window.scrollY > window.innerHeight) return;
      var i = (Math.random() * leaves.length) | 0;
      h1.classList.remove('glitching'); void h1.offsetWidth; h1.classList.add('glitching');
      decode(leaves[i], words[i], 520);
    }, 6500);
  }

  // ---------- typed subline, cycling ----------
  var typedBox = document.getElementById('typed'), typed = document.getElementById('typed-text');
  if (typedBox && typed) {
    var lines = typedBox.dataset.lines.split('|'), li = 0;
    var erase = function (done) {
      if (!typed.textContent.length) return done();
      typed.textContent = typed.textContent.slice(0, -1);
      setTimeout(function () { erase(done); }, 18);
    };
    var type = function (s, i, done) {
      if (i > s.length) return done();
      typed.textContent = s.slice(0, i);
      setTimeout(function () { type(s, i + 1, done); }, 38 + Math.random() * 40);
    };
    var cycle = function () {
      setTimeout(function () { erase(function () { li = (li + 1) % lines.length; type(lines[li], 0, cycle); }); }, 3200);
    };
    typed.textContent = '';
    setTimeout(function () { type(lines[0], 0, cycle); }, 1500);
  }

  // ---------- example strip: steps light up, cost counts, pull requests merge ----------
  var arrow = document.querySelector('.handoff .arrow-down');
  if (arrow) {
    arrow.innerHTML = arrow.textContent.split(' · ').map(function (w) { return '<span>' + w + '</span>'; }).join(' · ');
    var steps = arrow.querySelectorAll('span');
    var prTags = document.querySelectorAll('.handoff .pr .tag');
    var costs = document.querySelectorAll('.handoff .cost b');
    var finals = Array.prototype.map.call(costs, function (b) { return b.textContent; });
    var setPr = function (state) {
      prTags.forEach(function (t) { t.className = 'tag ' + (state === 'merged' ? 'proven' : 'working'); t.textContent = state; });
    };
    var runHandoff = function () {
      steps.forEach(function (s) { s.classList.remove('on'); });
      setPr('building');
      costs[0].textContent = '$0.00'; costs[1].textContent = '0 / 11';
      var k = 0;
      var iv = setInterval(function () {
        if (k < steps.length) {
          steps[k].classList.add('on'); k++;
          costs[0].textContent = '$' + (3.47 * k / steps.length).toFixed(2);
          costs[1].textContent = Math.round(11 * k / steps.length) + ' / 11';
          return;
        }
        clearInterval(iv);
        setPr('merged');
        costs.forEach(function (b, i) { b.textContent = finals[i]; });
        setTimeout(runHandoff, 4200);
      }, 700);
    };
    setTimeout(runHandoff, 2000);
  }

  // ---------- reveals: only what starts below the first screen, so the page is whole at rest ----------
  if (!('IntersectionObserver' in window)) return;
  var fold = window.innerHeight;
  var io = new IntersectionObserver(function (es) {
    es.forEach(function (e) {
      if (!e.isIntersecting) return;
      e.target.classList.add('in');
      var sec = e.target.closest('.ch, .prose'); if (sec) sec.classList.add('in');
      io.unobserve(e.target);
    });
  }, { threshold: 0.15 });
  document.querySelectorAll('.ch-head, .app, .notes, .stage, .steps, .plat, .prose-grid, .figures, .end h2').forEach(function (t) {
    if (t.getBoundingClientRect().top > fold) { t.classList.add('arm'); io.observe(t); }
  });
  document.querySelectorAll('.app-body').forEach(function (b) {
    if (b.getBoundingClientRect().top <= fold) return;
    b.classList.add('arm-seq');
    Array.prototype.forEach.call(b.children, function (c, i) { c.style.transitionDelay = (0.25 + i * 0.35) + 's'; });
    var o = new IntersectionObserver(function (es) { if (es[0].isIntersecting) { b.classList.add('in'); o.disconnect(); } }, { threshold: 0.25 });
    o.observe(b);
  });
})();
