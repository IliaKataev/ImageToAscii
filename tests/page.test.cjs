const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const vm = require('node:vm');

const root = path.join(__dirname, '../web/wwwroot');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const script = fs.readFileSync(path.join(root, 'scripts/app.js'), 'utf8');

function loadPage() {
  const elements = new Map();
  const requests = [];
  const timers = new Map();
  let timerId = 0;

  function element(selector) {
    if (!elements.has(selector)) {
      elements.set(selector, {
        attributes: new Map(),
        style: { setProperty() {} },
        setAttribute(name, value) { this.attributes.set(name, value); },
        addEventListener(name, handler) { this[name] = handler; },
      });
    }
    return elements.get(selector);
  }

  const slider = element('#detail');
  const range = html.match(/<input[^>]+id="detail"[^>]+>/)[0];
  slider.value = range.match(/\bvalue="([^"]+)"/)[1];
  vm.runInNewContext(script, {
    document: { querySelector: element },
    AbortController,
    FormData,
    setTimeout(handler) { timers.set(++timerId, handler); return timerId; },
    clearTimeout(id) { timers.delete(id); },
    fetch(url, options) {
      return new Promise(resolve => requests.push({ url, ...options, resolve }));
    },
  });

  return {
    slider,
    range,
    requests,
    result: element('#result'),
    label: element('#detail-value'),
    choose(file) {
      const input = element('#image');
      input.files = [file];
      input.change();
    },
    select(level) {
      slider.value = String(level);
      slider.input();
    },
    flush() {
      const callbacks = [...timers.values()];
      timers.clear();
      return callbacks.map(callback => callback());
    },
  };
}

test('selects four named levels and reuses the selected file', async () => {
  const page = loadPage();
  assert.match(page.range, /min="0" max="3" step="1"/);
  assert.match(page.range, /aria-valuetext="Средняя"/);
  assert.equal(page.slider.value, '1');
  assert.match(html, /id="detail-value"[^>]*>Средняя<\/output>/);
  const ticks = html.match(/class="detail-ticks"[^>]*>(.*?)<\/div>/)[1];
  assert.equal((ticks.match(/<i>/g) ?? []).length, 4);

  const file = new Blob(['image'], { type: 'image/png' });
  page.choose(file);
  const [initial] = page.flush();
  assert.equal(page.requests[0].body.get('width'), '96');
  page.requests[0].resolve({ ok: true, text: async () => '@@@' });
  await initial;
  assert.equal(page.result.textContent, '@@@');
  assert.equal(page.result.hidden, false);

  for (const [level, width, label] of [
    [0, '48', 'Низкая'], [1, '96', 'Средняя'], [2, '160', 'Высокая'], [3, '240', 'Максимальная'],
  ]) {
    page.select(level);
    assert.equal(page.label.textContent, label);
    assert.equal(page.slider.attributes.get('aria-valuetext'), label);
    const [conversion] = page.flush();
    const request = page.requests.at(-1);
    assert.equal(request.url, '/api/convert');
    assert.equal(request.method, 'POST');
    assert.equal(request.body.get('width'), width);
    assert.equal(await request.body.get('image').text(), 'image');
    request.resolve({ ok: true, text: async () => width });
    await conversion;
    assert.equal(page.result.textContent, width);
  }
});

test('coalesces rapid changes and ignores a late response', async () => {
  const page = loadPage();
  page.choose(new Blob(['image']));
  const [first] = page.flush();
  page.select(0);
  page.select(2);
  page.select(3);
  const conversions = page.flush();
  assert.equal(conversions.length, 1);
  assert.equal(page.requests.length, 2);
  assert.equal(page.requests[0].signal.aborted, true);
  assert.equal(page.requests[1].body.get('width'), '240');

  page.requests[1].resolve({ ok: true, text: async () => 'new' });
  await conversions[0];
  page.requests[0].resolve({ ok: true, text: async () => 'old' });
  await first;
  assert.equal(page.result.textContent, 'new');
});
