import { apiPost } from '../api';
export function renderEventForm(container) {
    container.innerHTML = `
    <h2 class="form-title">Новое событие</h2>
    <form id="event-form" novalidate>
      <div class="field">
        <label for="title">Название *</label>
        <input id="title" type="text" placeholder="Например: Встреча с врачом" required />
      </div>
      <div class="field">
        <label for="date">Дата *</label>
        <input id="date" type="text" placeholder="25 декабря 2025 или 25.12.2025 14:00" required />
        <span class="hint">Для ежегодного: «15 марта» или «15.03»</span>
      </div>
      <div class="field field-row">
        <input id="yearly" type="checkbox" />
        <label for="yearly">Повторять ежегодно</label>
      </div>
      <div class="field">
        <label for="description">Описание</label>
        <textarea id="description" rows="2" placeholder="Необязательно"></textarea>
      </div>
      <div class="field">
        <label for="place">Место</label>
        <input id="place" type="text" placeholder="Необязательно" />
      </div>
      <div class="field">
        <label for="link">Ссылка</label>
        <input id="link" type="url" placeholder="https://..." />
      </div>
      <p id="error" class="error hidden"></p>
    </form>
  `;
    const tg = window.Telegram?.WebApp;
    tg?.MainButton.setText('Добавить событие');
    tg?.MainButton.show();
    tg?.MainButton.enable();
    tg?.MainButton.onClick(async () => {
        const title = document.getElementById('title').value.trim();
        const date = document.getElementById('date').value.trim();
        const isYearly = document.getElementById('yearly').checked;
        const description = document.getElementById('description').value.trim() || null;
        const place = document.getElementById('place').value.trim() || null;
        const link = document.getElementById('link').value.trim() || null;
        const errorEl = document.getElementById('error');
        if (!title || !date) {
            errorEl.textContent = 'Заполните обязательные поля';
            errorEl.classList.remove('hidden');
            return;
        }
        tg?.MainButton.showProgress();
        tg?.MainButton.disable();
        errorEl.classList.add('hidden');
        try {
            await apiPost('/api/events', { title, date, isYearly, description, place, link });
            tg?.close();
        }
        catch (err) {
            errorEl.textContent = err instanceof Error ? err.message : 'Ошибка при сохранении';
            errorEl.classList.remove('hidden');
            tg?.MainButton.hideProgress();
            tg?.MainButton.enable();
        }
    });
}
