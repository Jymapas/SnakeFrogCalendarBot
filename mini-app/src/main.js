import { renderEventForm } from './forms/EventForm';
import { renderBirthdayForm } from './forms/BirthdayForm';
import './style.css';
function getFormParam() {
    return new URLSearchParams(window.location.search).get('form') ?? '';
}
function main() {
    const tg = window.Telegram?.WebApp;
    tg?.ready();
    tg?.expand();
    const container = document.getElementById('app');
    const form = getFormParam();
    if (form === 'event') {
        renderEventForm(container);
    }
    else if (form === 'birthday') {
        renderBirthdayForm(container);
    }
    else {
        container.innerHTML = `
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `;
    }
}
main();
