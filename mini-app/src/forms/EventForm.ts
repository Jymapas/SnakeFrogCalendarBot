import { apiPost } from '../api'

interface CreateEventRequest {
  title: string
  date: string
  isYearly: boolean
  description: string | null
  place: string | null
  link: string | null
}

function buildDateString(dateVal: string, timeVal: string, isAllDay: boolean, isYearly: boolean): string {
  if (isYearly) {
    // Extract dd.MM from yyyy-MM-dd
    const [, m, d] = dateVal.split('-')
    return `${d}.${m}`
  }
  if (isAllDay) {
    return dateVal // yyyy-MM-dd — RuDateTimeParser understands it
  }
  return `${dateVal} ${timeVal}` // yyyy-MM-dd HH:mm
}

export function renderEventForm(container: HTMLElement): void {
  const today = new Date().toISOString().split('T')[0]

  container.innerHTML = `
    <h2 class="form-title">Новое событие</h2>
    <form id="event-form" novalidate>
      <div class="field">
        <label for="title">Название *</label>
        <input id="title" type="text" placeholder="Например: Встреча с врачом" required />
      </div>

      <div class="field">
        <label for="date">Дата *</label>
        <input id="date" type="date" value="${today}" required />
      </div>

      <div class="field field-row">
        <input id="allday" type="checkbox" checked />
        <label for="allday">Весь день</label>
      </div>

      <div class="field" id="time-field" style="display:none">
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
    timeField.style.display = alldayEl.checked ? 'none' : 'flex'
  })

  const tg = window.Telegram?.WebApp
  tg?.MainButton.setText('Добавить событие')
  tg?.MainButton.show()
  tg?.MainButton.enable()

  tg?.MainButton.onClick(async () => {
    const title = (document.getElementById('title') as HTMLInputElement).value.trim()
    const dateVal = (document.getElementById('date') as HTMLInputElement).value
    const timeVal = (document.getElementById('time') as HTMLInputElement).value
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

    const date = buildDateString(dateVal, timeVal, isAllDay, isYearly)

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
  })
}
