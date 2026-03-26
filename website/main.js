// GitHub Stars
(function () {
  var el = document.getElementById('gh-stars');
  if (!el) return;
  fetch('https://api.github.com/repos/holgerleichsenring/agent-smith')
    .then(function (r) { return r.json(); })
    .then(function (data) {
      if (data.stargazers_count != null) {
        var count = data.stargazers_count;
        el.textContent = count >= 1000
          ? (count / 1000).toFixed(1).replace(/\.0$/, '') + 'k'
          : count;
      }
    })
    .catch(function () { /* silent */ });
})();

// Animated pipeline demo
(function () {
  var tabs = document.querySelectorAll('.demo-tab');
  var panels = document.querySelectorAll('.demo-panel');
  var progressBar = document.querySelector('.demo-progress-bar');
  var order = ['fix', 'api', 'legal', 'security', 'mad'];
  var current = 0;
  var autoTimer = null;
  var revealTimer = null;
  var progressTimer = null;

  // Delay per line in ms (faster for blanks/separators)
  var LINE_DELAY = 180;
  var PAUSE_AFTER = 2200;

  function clearTimers() {
    if (revealTimer) { clearTimeout(revealTimer); revealTimer = null; }
    if (autoTimer) { clearTimeout(autoTimer); autoTimer = null; }
    if (progressTimer) { clearInterval(progressTimer); progressTimer = null; }
  }

  function resetPanel(panel) {
    var lines = panel.querySelectorAll(':scope > div, :scope > span');
    lines.forEach(function (l) {
      l.classList.remove('revealed', 'cursor-line');
    });
  }

  function resetAllPanels() {
    panels.forEach(function (p) { resetPanel(p); });
  }

  function animateProgress(totalMs) {
    if (!progressBar) return;
    progressBar.style.transition = 'none';
    progressBar.style.width = '0%';
    // force reflow
    progressBar.offsetWidth;
    progressBar.style.transition = 'width ' + totalMs + 'ms linear';
    progressBar.style.width = '100%';
  }

  function revealLines(panel, onDone) {
    var lines = Array.prototype.slice.call(
      panel.querySelectorAll(':scope > div, :scope > span')
    );
    var i = 0;

    var totalMs = lines.length * LINE_DELAY + PAUSE_AFTER;
    animateProgress(totalMs);

    function next() {
      if (i > 0) {
        lines[i - 1].classList.remove('cursor-line');
      }
      if (i >= lines.length) {
        if (onDone) onDone();
        return;
      }
      var line = lines[i];
      line.classList.add('revealed');
      // Only show cursor on content lines (not blanks)
      if (line.tagName === 'DIV') {
        line.classList.add('cursor-line');
      }
      i++;
      // Blanks and separators are faster
      var isBlank = line.classList.contains('dblank') ||
                    line.querySelector('.dsep');
      revealTimer = setTimeout(next, isBlank ? 60 : LINE_DELAY);
    }

    next();
  }

  function switchTo(id, animate) {
    clearTimers();
    resetAllPanels();

    tabs.forEach(function (t) {
      t.classList.toggle('active', t.dataset.panel === id);
    });
    panels.forEach(function (p) {
      p.classList.toggle('active', p.id === 'panel-' + id);
    });

    var panel = document.getElementById('panel-' + id);
    if (!panel) return;

    if (animate !== false) {
      revealLines(panel, function () {
        autoTimer = setTimeout(autoAdvance, PAUSE_AFTER);
      });
    } else {
      // Instant reveal (no animation)
      var lines = panel.querySelectorAll(':scope > div, :scope > span');
      lines.forEach(function (l) { l.classList.add('revealed'); });
      autoTimer = setTimeout(autoAdvance, 3000);
    }
  }

  function autoAdvance() {
    current = (current + 1) % order.length;
    switchTo(order[current], true);
  }

  // Tab click handlers
  tabs.forEach(function (tab) {
    tab.addEventListener('click', function () {
      current = order.indexOf(tab.dataset.panel);
      switchTo(tab.dataset.panel, true);
    });
  });

  // Start first panel
  switchTo(order[0], true);
})();
