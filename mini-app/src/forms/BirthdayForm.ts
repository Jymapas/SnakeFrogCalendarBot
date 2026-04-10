import { apiPost } from '../api'

interface CreateBirthdayRequest {
  personName: string
  date: string
  birthYear: string | null
  contact: string | null
}

const MONTHS = [
  'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
  'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь',
]

function monthOptions(): string {
  return MONTHS.map((name, i) => `<option value="${i + 1}">${name}</option>`).join('')
}

function dayOptions(): string {
  return Array.from({ length: 31 }, (_, i) => `<option value="${i + 1}">${i + 1}</option>`).join('')
}

export function renderBirthdayForm(container: HTMLElement): void {
  const now = new Date()
  const curMonth = now.getMonth() + 1
  const curDay = now.getDate()
  const tg = window.Telegram?.WebApp

  container.innerHTML = `
    <h2 class="form-title">Новый день рождения</h2>
    <form id="birthday-form" novalidate>
      <div class="field">
        <label for="name">Имя *</label>
        <input id="name" type="text" placeholder="Например: Мама" required />
      </div>

      <div class="field">
        <label>Дата рождения *</label>
        <div class="date-row">
          <select id="day" class="select-day">
            ${dayOptions()}
          </select>
          <select id="month" class="select-month">
            ${monthOptions()}
          </select>
        </div>
      </div>

      <div class="field">
        <label for="year">Год рождения</label>
        <input id="year" type="number" placeholder="1997 (необязательно)" min="1900" max="2100" />
      </div>

      <div class="field">
        <label for="contact">Контакт в Telegram или ссылка</label>
        <input id="contact" type="text" placeholder="@username (необязательно)" />
      </div>
      <p id="error" class="error hidden"></p>
    </form>
  `

  const dayEl = document.getElementById('day') as HTMLSelectElement
  const monthEl = document.getElementById('month') as HTMLSelectElement
  dayEl.value = String(curDay)
  monthEl.value = String(curMonth)

  // BackButton
  tg?.BackButton.show()
  tg?.BackButton.onClick(() => history.back())

  async function submit(): Promise<void> {
    const personName = (document.getElementById('name') as HTMLInputElement).value.trim()
    const day = (document.getElementById('day') as HTMLSelectElement).value
    const month = (document.getElementById('month') as HTMLSelectElement).value
    const yearInput = (document.getElementById('year') as HTMLInputElement).value.trim()
    const contact = (document.getElementById('contact') as HTMLInputElement).value.trim() || null
    const errorEl = document.getElementById('error')!

    if (!personName) {
      errorEl.textContent = 'Заполните обязательные поля'
      errorEl.classList.remove('hidden')
      return
    }

    const dd = day.padStart(2, '0')
    const mm = month.padStart(2, '0')
    const date = yearInput ? `${dd}.${mm}.${yearInput}` : `${dd}.${mm}`

    tg?.MainButton.showProgress()
    tg?.MainButton.disable()
    errorEl.classList.add('hidden')

    try {
      await apiPost<CreateBirthdayRequest>('/api/birthdays', {
        personName,
        date,
        birthYear: yearInput || null,
        contact,
      })
      tg?.close()
    } catch (err) {
      errorEl.textContent = err instanceof Error ? err.message : 'Ошибка при сохранении'
      errorEl.classList.remove('hidden')
      tg?.MainButton.hideProgress()
      tg?.MainButton.enable()
    }
  }

  tg?.MainButton.setText('Добавить день рождения')
  tg?.MainButton.show()
  tg?.MainButton.enable()
  tg?.MainButton.onClick(submit)
}
