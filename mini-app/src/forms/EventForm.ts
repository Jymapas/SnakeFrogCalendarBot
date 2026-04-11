import { apiPost } from '../api'

interface CreateEventRequest {
  title: string
  date: string
  isYearly: boolean
  description: string | null
  place: string | null
  link: string | null
}

function formatDateDisplay(isoValue: string): string {
  if (!isoValue) return ''
  const [y, m, d] = isoValue.split('-')
  return `${d}.${m}.${y}`
}

function buildDateString(dateVal: string, hour: string, minute: string, isAllDay: boolean, isYearly: boolean): string {
  if (isYearly) {
    const [, m, d] = dateVal.split('-')
    return `${d}.${m}`
  }
  if (isAllDay) return dateVal
  const hh = hour.padStart(2, '0')
  const mm = minute.padStart(2, '0')
  return `${dateVal} ${hh}:${mm}`
}

function hourOptions(selected: number): string {
  return Array.from({ length: 24 }, (_, i) => {
    const v = String(i).padStart(2, '0')
    return `<option value="${i}"${i === selected ? ' selected' : ''}>${v}</option>`
  }).join('')
}

function minuteOptions(selected: number): string {
  return Array.from({ length: 60 }, (_, i) => {
    const v = String(i).padStart(2, '0')
    return `<option value="${i}"${i === selected ? ' selected' : ''}>${v}</option>`
  }).join('')
}

export function renderEventForm(container: HTMLElement): void {
  const today = new Date().toISOString().split('T')[0]
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
        <div class="date-picker-wrapper">
          <div class="date-display" id="date-display">${formatDateDisplay(today)}</div>
          <input id="date" type="date" value="${today}" />
        </div>
      </div>

      <div class="field field-row">
        <input id="allday" type="checkbox" />
        <label for="allday">Весь день</label>
      </div>

      <div class="field" id="time-field">
        <label>Время</label>
        <div class="time-row">
          <select id="hour" class="select-hour">
            ${hourOptions(12)}
          </select>
          <span class="date-separator">:</span>
          <select id="minute" class="select-minute">
            ${minuteOptions(0)}
          </select>
        </div>
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

  const dateInput = document.getElementById('date') as HTMLInputElement
  const dateDisplay = document.getElementById('date-display') as HTMLElement
  dateInput.addEventListener('change', () => {
    dateDisplay.textContent = formatDateDisplay(dateInput.value)
  })

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
    const dateVal = (document.getElementById('date') as HTMLInputElement).value
    const hour = (document.getElementById('hour') as HTMLSelectElement).value
    const minute = (document.getElementById('minute') as HTMLSelectElement).value
    const isAllDay = (document.getElementById('allday') as HTMLInputElement).checked
    const isYearly = (document.getElementById('yearly') as HTMLInputElement).checked
    const description = (document.getElementById('description') as HTMLTextAreaElement).value.trim() || null
    const place = (document.getElementById('place') as HTMLInputElement).value.trim() || null
    const link = (document.getElementById('link') as HTMLInputElement).value.trim() || null
    const errorEl = document.getElementById('error')!

    if (!title || !dateVal) {
      errorEl.textContent = 'Заполните обязательные поля'
      errorEl.classList.remove('hidden')
      return
    }

    const date = buildDateString(dateVal, hour, minute, isAllDay, isYearly)

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
