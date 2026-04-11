import { apiPost } from '../api'

interface CreateEventRequest {
  title: string
  date: string
  isYearly: boolean
  description: string | null
  place: string | null
  link: string | null
}

const MONTHS = [
  'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
  'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь',
]

function monthOptions(selected: number): string {
  return MONTHS.map((name, i) => {
    const val = i + 1
    return `<option value="${val}"${val === selected ? ' selected' : ''}>${name}</option>`
  }).join('')
}

function dayOptions(selected: number): string {
  return Array.from({ length: 31 }, (_, i) => {
    const val = i + 1
    return `<option value="${val}"${val === selected ? ' selected' : ''}>${val}</option>`
  }).join('')
}

function yearOptions(selected: number): string {
  const options: string[] = []
  for (let y = selected - 1; y <= selected + 5; y++) {
    options.push(`<option value="${y}"${y === selected ? ' selected' : ''}>${y}</option>`)
  }
  return options.join('')
}

function buildDateString(day: string, month: string, year: string, timeVal: string, isAllDay: boolean, isYearly: boolean): string {
  const dd = day.padStart(2, '0')
  const mm = month.padStart(2, '0')
  if (isYearly) return `${dd}.${mm}`
  if (isAllDay) return `${year}-${mm}-${dd}`
  return `${year}-${mm}-${dd} ${timeVal}`
}

export function renderEventForm(container: HTMLElement): void {
  const now = new Date()
  const curDay = now.getDate()
  const curMonth = now.getMonth() + 1
  const curYear = now.getFullYear()
  const tg = window.Telegram?.WebApp

  container.innerHTML = `
    <h2 class="form-title">Новое событие</h2>
    <form id="event-form" novalidate>
      <div class="field">
        <label for="title">Название *</label>
        <input id="title" type="text" placeholder="Например: Встреча с врачом" required />
      </div>

      <div class="field">
        <label>Дата *</label>
        <div class="date-row">
          <select id="day" class="select-day">
            ${dayOptions(curDay)}
          </select>
          <span class="date-separator">.</span>
          <select id="month" class="select-month-short">
            ${monthOptions(curMonth)}
          </select>
          <span class="date-separator">.</span>
          <select id="year" class="select-year">
            ${yearOptions(curYear)}
          </select>
        </div>
      </div>

      <div class="field field-row">
        <input id="allday" type="checkbox" />
        <label for="allday">Весь день</label>
      </div>

      <div class="field" id="time-field">
        <label for="time">Время</label>
        <input id="time" type="time" value="12:00" />
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
  `

  const alldayEl = document.getElementById('allday') as HTMLInputElement
  const timeField = document.getElementById('time-field') as HTMLElement
  alldayEl.addEventListener('change', () => {
    timeField.style.display = alldayEl.checked ? 'none' : ''
  })

  // BackButton
  tg?.BackButton.show()
  tg?.BackButton.onClick(() => { window.location.href = window.location.pathname })

  async function submit(): Promise<void> {
    const title = (document.getElementById('title') as HTMLInputElement).value.trim()
    const day = (document.getElementById('day') as HTMLSelectElement).value
    const month = (document.getElementById('month') as HTMLSelectElement).value
    const year = (document.getElementById('year') as HTMLSelectElement).value
    const timeVal = (document.getElementById('time') as HTMLInputElement).value
    const isAllDay = (document.getElementById('allday') as HTMLInputElement).checked
    const isYearly = (document.getElementById('yearly') as HTMLInputElement).checked
    const description = (document.getElementById('description') as HTMLTextAreaElement).value.trim() || null
    const place = (document.getElementById('place') as HTMLInputElement).value.trim() || null
    const link = (document.getElementById('link') as HTMLInputElement).value.trim() || null
    const errorEl = document.getElementById('error')!

    if (!title) {
      errorEl.textContent = 'Заполните обязательные поля'
      errorEl.classList.remove('hidden')
      return
    }

    const date = buildDateString(day, month, year, timeVal, isAllDay, isYearly)

    tg?.MainButton.showProgress()
    tg?.MainButton.disable()
    errorEl.classList.add('hidden')

    try {
      await apiPost<CreateEventRequest>('/api/events', { title, date, isYearly, description, place, link })
      tg?.close()
    } catch (err) {
      errorEl.textContent = err instanceof Error ? err.message : 'Ошибка при сохранении'
      errorEl.classList.remove('hidden')
      tg?.MainButton.hideProgress()
      tg?.MainButton.enable()
    }
  }

  tg?.MainButton.setText('Добавить событие')
  tg?.MainButton.show()
  tg?.MainButton.enable()
  tg?.MainButton.onClick(submit)
}
