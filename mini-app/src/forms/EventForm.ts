import { apiPost } from '../api'

type Recurrence = 'oneoff' | 'yearly' | 'weekly' | 'interval'

interface CreateEventRequest {
  title: string
  date: string
  isYearly: boolean
  recurrence: Recurrence
  daysOfWeek: number[] | null
  intervalCount: number | null
  intervalUnit: string | null
  repeatUntil: string | null
  description: string | null
  place: string | null
  link: string | null
}

function formatDateDisplay(isoValue: string): string {
  if (!isoValue) return ''
  const [y, m, d] = isoValue.split('-')
  return `${d}.${m}.${y}`
}

function buildDateString(
  dateVal: string,
  hour: string,
  minute: string,
  isAllDay: boolean,
  recurrence: Recurrence,
): string {
  // Ежегодное событие бэкенд читает парсером дня и месяца.
  if (recurrence === 'yearly') {
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

const WEEKDAYS = [
  { value: 1, label: 'Пн' },
  { value: 2, label: 'Вт' },
  { value: 3, label: 'Ср' },
  { value: 4, label: 'Чт' },
  { value: 5, label: 'Пт' },
  { value: 6, label: 'Сб' },
  { value: 7, label: 'Вс' },
]

function weekdayCheckboxes(): string {
  return WEEKDAYS.map(
    (day) => `
      <label class="weekday">
        <input type="checkbox" class="weekday-input" value="${day.value}" />
        <span>${day.label}</span>
      </label>`,
  ).join('')
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
        <label for="recurrence">Повторение</label>
        <select id="recurrence">
          <option value="oneoff" selected>Разовое</option>
          <option value="yearly">Ежегодно</option>
          <option value="weekly">Еженедельно</option>
          <option value="interval">Раз в N периодов</option>
        </select>
      </div>

      <div class="field">
        <label id="date-label">Дата *</label>
        <div class="date-picker-wrapper">
          <div class="date-display" id="date-display">${formatDateDisplay(today)}</div>
          <input id="date" type="date" value="${today}" />
        </div>
      </div>

      <div class="field hidden" id="weekdays-field">
        <label>Дни недели *</label>
        <div class="weekday-row">${weekdayCheckboxes()}</div>
      </div>

      <div class="field hidden" id="interval-field">
        <label for="interval-count">Повторять каждые</label>
        <div class="time-row">
          <input id="interval-count" type="number" min="1" step="1" value="1" class="select-hour" />
          <select id="interval-unit" class="select-minute">
            <option value="days">дни</option>
            <option value="weeks" selected>недели</option>
            <option value="months">месяцы</option>
            <option value="years">годы</option>
          </select>
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

      <div class="field hidden" id="repeat-until-field">
        <label for="repeat-until">Повторять до (необязательно)</label>
        <input id="repeat-until" type="date" />
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
  dateInput.addEventListener('input', () => {
    dateDisplay.textContent = formatDateDisplay(dateInput.value)
  })

  const alldayEl = document.getElementById('allday') as HTMLInputElement
  const timeField = document.getElementById('time-field') as HTMLElement
  alldayEl.addEventListener('change', () => {
    timeField.style.display = alldayEl.checked ? 'none' : ''
  })

  const recurrenceEl = document.getElementById('recurrence') as HTMLSelectElement
  const weekdaysField = document.getElementById('weekdays-field') as HTMLElement
  const intervalField = document.getElementById('interval-field') as HTMLElement
  const repeatUntilField = document.getElementById('repeat-until-field') as HTMLElement
  const dateLabel = document.getElementById('date-label') as HTMLElement

  function currentRecurrence(): Recurrence {
    return recurrenceEl.value as Recurrence
  }

  function syncRecurrenceFields(): void {
    const recurrence = currentRecurrence()
    const isRecurring = recurrence === 'weekly' || recurrence === 'interval'

    weekdaysField.classList.toggle('hidden', recurrence !== 'weekly')
    intervalField.classList.toggle('hidden', recurrence !== 'interval')
    repeatUntilField.classList.toggle('hidden', !isRecurring)
    dateLabel.textContent = isRecurring ? 'Дата начала *' : 'Дата *'
  }

  recurrenceEl.addEventListener('change', syncRecurrenceFields)
  syncRecurrenceFields()

  // BackButton
  tg?.BackButton.show()
  tg?.BackButton.onClick(() => { window.location.href = window.location.pathname })

  async function submit(): Promise<void> {
    const title = (document.getElementById('title') as HTMLInputElement).value.trim()
    const dateVal = (document.getElementById('date') as HTMLInputElement).value
    const hour = (document.getElementById('hour') as HTMLSelectElement).value
    const minute = (document.getElementById('minute') as HTMLSelectElement).value
    const isAllDay = alldayEl.checked
    const recurrence = currentRecurrence()
    const description = (document.getElementById('description') as HTMLTextAreaElement).value.trim() || null
    const place = (document.getElementById('place') as HTMLInputElement).value.trim() || null
    const link = (document.getElementById('link') as HTMLInputElement).value.trim() || null
    const repeatUntil = (document.getElementById('repeat-until') as HTMLInputElement).value || null
    const errorEl = document.getElementById('error')!

    const daysOfWeek = Array.from(
      document.querySelectorAll<HTMLInputElement>('.weekday-input:checked'),
    ).map((input) => Number(input.value))

    const intervalCount = Number((document.getElementById('interval-count') as HTMLInputElement).value)
    const intervalUnit = (document.getElementById('interval-unit') as HTMLSelectElement).value

    function fail(message: string): void {
      errorEl.textContent = message
      errorEl.classList.remove('hidden')
    }

    if (!title || !dateVal) {
      fail('Заполните обязательные поля')
      return
    }

    if (recurrence === 'weekly' && daysOfWeek.length === 0) {
      fail('Выберите хотя бы один день недели')
      return
    }

    if (recurrence === 'interval' && (!Number.isInteger(intervalCount) || intervalCount < 1)) {
      fail('Интервал должен быть целым числом не меньше 1')
      return
    }

    if (repeatUntil && repeatUntil < dateVal) {
      fail('Дата окончания не может быть раньше даты начала')
      return
    }

    const date = buildDateString(dateVal, hour, minute, isAllDay, recurrence)

    tg?.MainButton.showProgress()
    tg?.MainButton.disable()
    errorEl.classList.add('hidden')

    try {
      await apiPost<CreateEventRequest>('/api/events', {
        title,
        date,
        // Оставлено для совместимости со старым контрактом бэкенда.
        isYearly: recurrence === 'yearly',
        recurrence,
        daysOfWeek: recurrence === 'weekly' ? daysOfWeek : null,
        intervalCount: recurrence === 'interval' ? intervalCount : null,
        intervalUnit: recurrence === 'interval' ? intervalUnit : null,
        repeatUntil: recurrence === 'weekly' || recurrence === 'interval' ? repeatUntil : null,
        description,
        place,
        link,
      })
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
