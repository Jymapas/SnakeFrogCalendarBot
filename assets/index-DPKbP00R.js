(function(){const o=document.createElement("link").relList;if(o&&o.supports&&o.supports("modulepreload"))return;for(const t of document.querySelectorAll('link[rel="modulepreload"]'))a(t);new MutationObserver(t=>{for(const i of t)if(i.type==="childList")for(const l of i.addedNodes)l.tagName==="LINK"&&l.rel==="modulepreload"&&a(l)}).observe(document,{childList:!0,subtree:!0});function e(t){const i={};return t.integrity&&(i.integrity=t.integrity),t.referrerPolicy&&(i.referrerPolicy=t.referrerPolicy),t.crossOrigin==="use-credentials"?i.credentials="include":t.crossOrigin==="anonymous"?i.credentials="omit":i.credentials="same-origin",i}function a(t){if(t.ep)return;t.ep=!0;const i=e(t);fetch(t.href,i)}})();const I="https://calendarpi.tail900a1a.ts.net".replace(/\/$/,"");function M(){var n,o;return((o=(n=window.Telegram)==null?void 0:n.WebApp)==null?void 0:o.initData)??""}async function E(n,o){const e=await fetch(`${I}${n}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${M()}`},body:JSON.stringify(o)});if(!e.ok){const a=await e.text().catch(()=>e.statusText);throw new Error(a||`HTTP ${e.status}`)}}function w(n){if(!n)return"";const[o,e,a]=n.split("-");return`${a}.${e}.${o}`}function k(n,o,e,a,t){if(t){const[,c,s]=n.split("-");return`${s}.${c}`}if(a)return n;const i=o.padStart(2,"0"),l=e.padStart(2,"0");return`${n} ${i}:${l}`}function x(n){return Array.from({length:24},(o,e)=>{const a=String(e).padStart(2,"0");return`<option value="${e}"${e===n?" selected":""}>${a}</option>`}).join("")}function L(n){return Array.from({length:60},(o,e)=>{const a=String(e).padStart(2,"0");return`<option value="${e}"${e===n?" selected":""}>${a}</option>`}).join("")}function S(n){var s;const o=new Date().toISOString().split("T")[0],e=(s=window.Telegram)==null?void 0:s.WebApp;n.innerHTML=`
    <h2 class="form-title">Новое событие</h2>
    <form id="event-form" novalidate>
      <div class="field">
        <label for="title">Название *</label>
        <input id="title" type="text" placeholder="Например: Встреча с врачом" required />
      </div>

      <div class="field">
        <label>Дата *</label>
        <div class="date-picker-wrapper">
          <div class="date-display" id="date-display">${w(o)}</div>
          <input id="date" type="date" value="${o}" tabindex="-1" />
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
            ${x(12)}
          </select>
          <span class="date-separator">:</span>
          <select id="minute" class="select-minute">
            ${L(0)}
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
  `;const a=document.getElementById("date"),t=document.getElementById("date-display");t.addEventListener("click",()=>{try{a.showPicker()}catch{a.click()}}),a.addEventListener("change",()=>{t.textContent=w(a.value)});const i=document.getElementById("allday"),l=document.getElementById("time-field");i.addEventListener("change",()=>{l.style.display=i.checked?"none":""}),e==null||e.BackButton.show(),e==null||e.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function c(){const u=document.getElementById("title").value.trim(),p=document.getElementById("date").value,h=document.getElementById("hour").value,m=document.getElementById("minute").value,v=document.getElementById("allday").checked,r=document.getElementById("yearly").checked,f=document.getElementById("description").value.trim()||null,y=document.getElementById("place").value.trim()||null,b=document.getElementById("link").value.trim()||null,d=document.getElementById("error");if(!u||!p){d.textContent="Заполните обязательные поля",d.classList.remove("hidden");return}const $=k(p,h,m,v,r);e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),d.classList.add("hidden");try{await E("/api/events",{title:u,date:$,isYearly:r,description:f,place:y,link:b}),e==null||e.close()}catch(B){d.textContent=B instanceof Error?B.message:"Ошибка при сохранении",d.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}}e==null||e.MainButton.setText("Добавить событие"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(c)}const T=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function P(){return T.map((n,o)=>`<option value="${o+1}">${n}</option>`).join("")}function O(){return Array.from({length:31},(n,o)=>`<option value="${o+1}">${o+1}</option>`).join("")}function C(n){var s;const o=new Date,e=o.getMonth()+1,a=o.getDate(),t=(s=window.Telegram)==null?void 0:s.WebApp;n.innerHTML=`
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
            ${O()}
          </select>
          <select id="month" class="select-month">
            ${P()}
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
  `;const i=document.getElementById("day"),l=document.getElementById("month");i.value=String(a),l.value=String(e),t==null||t.BackButton.show(),t==null||t.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function c(){const u=document.getElementById("name").value.trim(),p=document.getElementById("day").value,h=document.getElementById("month").value,m=document.getElementById("year").value.trim(),v=document.getElementById("contact").value.trim()||null,r=document.getElementById("error");if(!u){r.textContent="Заполните обязательные поля",r.classList.remove("hidden");return}const f=p.padStart(2,"0"),y=h.padStart(2,"0"),b=m?`${f}.${y}.${m}`:`${f}.${y}`;t==null||t.MainButton.showProgress(),t==null||t.MainButton.disable(),r.classList.add("hidden");try{await E("/api/birthdays",{personName:u,date:b,birthYear:m||null,contact:v}),t==null||t.close()}catch(d){r.textContent=d instanceof Error?d.message:"Ошибка при сохранении",r.classList.remove("hidden"),t==null||t.MainButton.hideProgress(),t==null||t.MainButton.enable()}}t==null||t.MainButton.setText("Добавить день рождения"),t==null||t.MainButton.show(),t==null||t.MainButton.enable(),t==null||t.MainButton.onClick(c)}function A(){return new URLSearchParams(window.location.search).get("form")??""}function D(){var a;const n=(a=window.Telegram)==null?void 0:a.WebApp;n==null||n.ready(),n==null||n.expand();const o=document.getElementById("app"),e=A();e==="event"?S(o):e==="birthday"?C(o):(n==null||n.BackButton.show(),n==null||n.BackButton.onClick(()=>n==null?void 0:n.close()),o.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `)}D();
