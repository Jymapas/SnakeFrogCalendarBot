import { renderEventForm } from './forms/EventForm'
import { renderBirthdayForm } from './forms/BirthdayForm'
import './style.css'

function getFormParam(): string {
  return new URLSearchParams(window.location.search).get('form') ?? ''
}

function main(): void {
  const tg = window.Telegram?.WebApp
  tg?.ready()
  tg?.expand()

  const container = document.getElementById('app')!

  // Debug: show initData state if empty
  if (!tg?.initData) {
    container.innerHTML = `
      <div style="padding:16px;font-family:monospace;font-size:13px">
        <b style="color:red">⚠ initData пустой</b><br><br>
        Telegram defined: ${window.Telegram !== undefined}<br>
        WebApp defined: ${window.Telegram?.WebApp !== undefined}<br>
        platform: ${tg?.platform ?? 'n/a'}<br>
        version: ${tg?.version ?? 'n/a'}<br>
        initData: "${tg?.initData ?? 'undefined'}"<br>
      </div>
    `
    return
  }

  const form = getFormParam()

  if (form === 'event') {
    renderEventForm(container)
  } else if (form === 'birthday') {
    renderBirthdayForm(container)
  } else {
    tg?.BackButton.show()
    tg?.BackButton.onClick(() => tg?.close())
    container.innerHTML = `
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `
  }
}

main()
