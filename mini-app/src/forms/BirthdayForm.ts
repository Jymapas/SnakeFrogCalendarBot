import { apiPost } from '../api'

interface CreateBirthdayRequest {
  personName: string
  date: string
  birthYear: string | null
  contact: string | null
}

export function renderBirthdayForm(container: HTMLElement): void {
  container.innerHTML = `
    <h2 class="form-title">Новый день рождения</h2>
    <form id="birthday-form" novalidate>
      <div class="field">
        <label for="name">Имя *</label>
        <input id="name" type="text" placeholder="Например: Мама" required />
      </div>
      <div class="field">
        <label for="date">Дата рождения *</label>
        <input id="date" type="text" placeholder="15 марта или 15.03" required />
        <span class="hint">Только день и месяц, год — необязателен</span>
      </div>
      <div class="field">
        <label for="year">Год рождения</label>
        <input id="year" type="number" placeholder="1990" min="1900" max="2100" />
      </div>
      <div class="field">
        <label for="contact">Контакт в Telegram</label>
        <input id="contact" type="text" placeholder="@username (необязательно)" />
      </div>
      <p id="error" class="error hidden"></p>
    </form>
  `

  const tg = window.Telegram?.WebApp
  tg?.MainButton.setText('Добавить день рождения')
  tg?.MainButton.show()
  tg?.MainButton.enable()

  tg?.MainButton.onClick(async () => {
    const personName = (document.getElementById('name') as HTMLInputElement).value.trim()
    const date = (document.getElementById('date') as HTMLInputElement).value.trim()
    const birthYear = (document.getElementById('year') as HTMLInputElement).value.trim() || null
    const contact = (document.getElementById('contact') as HTMLInputElement).value.trim() || null
    const errorEl = document.getElementById('error')!

    if (!personName || !date) {
      errorEl.textContent = 'Заполните обязательные поля'
      errorEl.classList.remove('hidden')
      return
    }

    tg?.MainButton.showProgress()
    tg?.MainButton.disable()
    errorEl.classList.add('hidden')

    try {
      await apiPost<CreateBirthdayRequest>('/api/birthdays', { personName, date, birthYear, contact })
      tg?.close()
    } catch (err) {
      errorEl.textContent = err instanceof Error ? err.message : 'Ошибка при сохранении'
      errorEl.classList.remove('hidden')
      tg?.MainButton.hideProgress()
      tg?.MainButton.enable()
    }
  })
}
