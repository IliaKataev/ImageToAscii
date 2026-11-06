const input = document.querySelector('#image');
const detail = document.querySelector('#detail');
const detailValue = document.querySelector('#detail-value');
const output = document.querySelector('.output');
const placeholder = document.querySelector('#placeholder');
const result = document.querySelector('#result');
const status = document.querySelector('#status');
const detailLevels = [
  { width: 48, label: 'Низкая' },
  { width: 96, label: 'Средняя' },
  { width: 160, label: 'Высокая' },
  { width: 240, label: 'Максимальная' },
];
let image;
let pending;
let timer;

function scheduleConversion(delay) {
  if (!image) return;
  pending?.abort();
  clearTimeout(timer);
  timer = setTimeout(convert, delay);
}

async function convert() {
  const request = new AbortController();
  pending = request;
  const { width } = detailLevels[Number(detail.value)];
  const body = new FormData();
  body.set('image', image);
  body.set('width', width);
  status.hidden = true;
  output.setAttribute('aria-busy', 'true');

  try {
    const response = await fetch('/api/convert', { method: 'POST', body, signal: request.signal });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      throw new Error(problem?.title ?? (response.status === 413
        ? 'Файл должен быть не больше 8 МиБ.'
        : 'Не удалось преобразовать изображение.'));
    }
    const ascii = await response.text();
    if (request.signal.aborted) return;
    result.style.setProperty('--columns', width);
    result.textContent = ascii;
    result.hidden = false;
  } catch (error) {
    if (request.signal.aborted) return;
    result.hidden = true;
    status.textContent = error instanceof TypeError ? 'Не удалось связаться с сервером.' : error.message;
    status.hidden = false;
  } finally {
    if (!request.signal.aborted) {
      placeholder.hidden = true;
      output.setAttribute('aria-busy', 'false');
    }
  }
}

input.addEventListener('change', () => {
  if (!input.files[0]) return;
  image = input.files[0];
  input.value = '';
  placeholder.hidden = false;
  result.hidden = true;
  status.hidden = true;
  scheduleConversion(0);
});

detail.addEventListener('input', () => {
  const { label } = detailLevels[Number(detail.value)];
  detailValue.textContent = label;
  detail.setAttribute('aria-valuetext', label);
  scheduleConversion(180);
});
