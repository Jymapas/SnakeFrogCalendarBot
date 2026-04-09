(function(){const e=document.createElement("link").relList;if(e&&e.supports&&e.supports("modulepreload"))return;for(const t of document.querySelectorAll('link[rel="modulepreload"]'))a(t);new MutationObserver(t=>{for(const i of t)if(i.type==="childList")for(const o of i.addedNodes)o.tagName==="LINK"&&o.rel==="modulepreload"&&a(o)}).observe(document,{childList:!0,subtree:!0});function r(t){const i={};return t.integrity&&(i.integrity=t.integrity),t.referrerPolicy&&(i.referrerPolicy=t.referrerPolicy),t.crossOrigin==="use-credentials"?i.credentials="include":t.crossOrigin==="anonymous"?i.credentials="omit":i.credentials="same-origin",i}function a(t){if(t.ep)return;t.ep=!0;const i=r(t);fetch(t.href,i)}})();const m="https://snakefrog.duckdns.org";function p(){var n,e;return((e=(n=window.Telegram)==null?void 0:n.WebApp)==null?void 0:e.initData)??""}async function u(n,e){const r=await fetch(`${m}${n}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${p()}`},body:JSON.stringify(e)});if(!r.ok){const a=await r.text().catch(()=>r.statusText);throw new Error(a||`HTTP ${r.status}`)}}function f(n){var r;n.innerHTML=`
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
  `;const e=(r=window.Telegram)==null?void 0:r.WebApp;e==null||e.MainButton.setText("Добавить событие"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(async()=>{const a=document.getElementById("title").value.trim(),t=document.getElementById("date").value.trim(),i=document.getElementById("yearly").checked,o=document.getElementById("description").value.trim()||null,l=document.getElementById("place").value.trim()||null,d=document.getElementById("link").value.trim()||null,s=document.getElementById("error");if(!a||!t){s.textContent="Заполните обязательные поля",s.classList.remove("hidden");return}e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),s.classList.add("hidden");try{await u("/api/events",{title:a,date:t,isYearly:i,description:o,place:l,link:d}),e==null||e.close()}catch(c){s.textContent=c instanceof Error?c.message:"Ошибка при сохранении",s.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}})}function h(n){var r;n.innerHTML=`
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
  `;const e=(r=window.Telegram)==null?void 0:r.WebApp;e==null||e.MainButton.setText("Добавить день рождения"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(async()=>{const a=document.getElementById("name").value.trim(),t=document.getElementById("date").value.trim(),i=document.getElementById("year").value.trim()||null,o=document.getElementById("contact").value.trim()||null,l=document.getElementById("error");if(!a||!t){l.textContent="Заполните обязательные поля",l.classList.remove("hidden");return}e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),l.classList.add("hidden");try{await u("/api/birthdays",{personName:a,date:t,birthYear:i,contact:o}),e==null||e.close()}catch(d){l.textContent=d instanceof Error?d.message:"Ошибка при сохранении",l.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}})}function y(){return new URLSearchParams(window.location.search).get("form")??""}function v(){var a;const n=(a=window.Telegram)==null?void 0:a.WebApp;n==null||n.ready(),n==null||n.expand();const e=document.getElementById("app"),r=y();r==="event"?f(e):r==="birthday"?h(e):e.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `}v();
