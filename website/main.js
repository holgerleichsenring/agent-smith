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
    .catch(function () { /* silent — badge just stays hidden */ });
})();

// Tab switching for pipeline demo
var tabs = document.querySelectorAll('.demo-tab');
var panels = document.querySelectorAll('.demo-panel');
var autoTimer;

function switchTo(id) {
  tabs.forEach(function (t) { t.classList.toggle('active', t.dataset.panel === id); });
  panels.forEach(function (p) { p.classList.toggle('active', p.id === 'panel-' + id); });
}

tabs.forEach(function (tab) {
  tab.addEventListener('click', function () {
    clearInterval(autoTimer);
    switchTo(tab.dataset.panel);
    autoTimer = setInterval(autoAdvance, 4000);
  });
});

var order = ['fix', 'api', 'legal', 'security', 'mad'];
var current = 0;

function autoAdvance() {
  current = (current + 1) % order.length;
  switchTo(order[current]);
}

autoTimer = setInterval(autoAdvance, 4000);
