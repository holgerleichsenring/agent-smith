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

  var life = document.getElementById('life');
  if (reduce) {
    if (life && life.pauseAnimations) { life.pauseAnimations(); document.getElementById('packets').style.display = 'none'; }
    return;
  }

  // ---------- the run line: a real run replayed, step by step, then looped ----------
  // The HTML holds the finished run, so reduced motion and no script show it whole.
  var run = document.getElementById('replay');
  var lineEl = document.getElementById('run-line');
  var logEl = document.getElementById('run-log');
  var clock = document.getElementById('run-clock');
  if (run && lineEl && logEl && clock) {
    var nodes = Array.prototype.slice.call(lineEl.children);
    var entries = Array.prototype.map.call(logEl.children, function (d) {
      return { step: +d.dataset.step, cls: d.className, t: d.querySelector('time').textContent, text: d.querySelector('span').textContent };
    });
    var finalClock = clock.textContent;
    var visible = true;
    if ('IntersectionObserver' in window) new IntersectionObserver(function (es) { visible = es[0].isIntersecting; }).observe(run);
    var later = function (fn, ms) {
      setTimeout(function wait() { if (visible && !document.hidden) fn(); else setTimeout(wait, 400); }, ms);
    };
    var light = function (step) {
      nodes.forEach(function (n, i) {
        n.classList.toggle('lit', i <= step);
        n.classList.toggle('now', i === step);
      });
      clock.textContent = 'running · step ' + (step + 1) + ' of ' + nodes.length;
    };
    var typeLine = function (e, done) {
      var row = document.createElement('div');
      row.className = (e.cls ? e.cls + ' ' : '') + 'typing';
      var t = document.createElement('time'); t.textContent = e.t;
      var sp = document.createElement('span');
      row.appendChild(t); row.appendChild(sp); logEl.appendChild(row);
      while (logEl.children.length > 8) logEl.removeChild(logEl.firstChild);
      var i = 0;
      (function tick() {
        sp.textContent = e.text.slice(0, ++i);
        if (i < e.text.length) later(tick, 16 + Math.random() * 22);
        else { row.classList.remove('typing'); later(done, 380); }
      })();
    };
    var playStep = function (step) {
      if (step >= nodes.length) {
        nodes.forEach(function (n) { n.classList.remove('now'); });
        clock.textContent = finalClock;
        run.classList.remove('playing'); run.classList.add('done');
        later(start, 5200);
        return;
      }
      light(step);
      var mine = entries.filter(function (e) { return e.step === step; });
      var k = 0;
      (function next() {
        if (k < mine.length) typeLine(mine[k++], next);
        else later(function () { playStep(step + 1); }, 650);
      })();
    };
    var start = function () {
      logEl.innerHTML = '';
      run.classList.remove('done'); run.classList.add('playing');
      nodes.forEach(function (n) { n.classList.remove('lit', 'now'); });
      later(function () { playStep(0); }, 500);
    };
    start();
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
