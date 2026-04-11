(function(){const o=document.createElement("link").relList;if(o&&o.supports&&o.supports("modulepreload"))return;for(const e of document.querySelectorAll('link[rel="modulepreload"]'))l(e);new MutationObserver(e=>{for(const t of e)if(t.type==="childList")for(const i of t.addedNodes)i.tagName==="LINK"&&i.rel==="modulepreload"&&l(i)}).observe(document,{childList:!0,subtree:!0});function a(e){const t={};return e.integrity&&(t.integrity=e.integrity),e.referrerPolicy&&(t.referrerPolicy=e.referrerPolicy),e.crossOrigin==="use-credentials"?t.credentials="include":e.crossOrigin==="anonymous"?t.credentials="omit":t.credentials="same-origin",t}function l(e){if(e.ep)return;e.ep=!0;const t=a(e);fetch(e.href,t)}})();const M="https://calendarpi.tail900a1a.ts.net".replace(/\/$/,"");function x(){var n,o;return((o=(n=window.Telegram)==null?void 0:n.WebApp)==null?void 0:o.initData)??""}async function $(n,o){const a=await fetch(`${M}${n}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${x()}`},body:JSON.stringify(o)});if(!a.ok){const l=await a.text().catch(()=>a.statusText);throw new Error(l||`HTTP ${a.status}`)}}const k=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function L(n){return k.map((o,a)=>{const l=a+1;return`<option value="${l}"${l===n?" selected":""}>${o}</option>`}).join("")}function T(n){return Array.from({length:31},(o,a)=>{const l=a+1;return`<option value="${l}"${l===n?" selected":""}>${l}</option>`}).join("")}function O(n){const o=[];for(let a=n-1;a<=n+5;a++)o.push(`<option value="${a}"${a===n?" selected":""}>${a}</option>`);return o.join("")}function P(n,o,a,l,e,t){const i=n.padStart(2,"0"),r=o.padStart(2,"0");return t?`${i}.${r}`:e?`${a}-${r}-${i}`:`${a}-${r}-${i} ${l}`}function S(n){var c;const o=new Date,a=o.getDate(),l=o.getMonth()+1,e=o.getFullYear(),t=(c=window.Telegram)==null?void 0:c.WebApp;n.innerHTML=`
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
            ${T(a)}
          </select>
          <span class="date-separator">.</span>
          <select id="month" class="select-month-short">
            ${L(l)}
          </select>
          <span class="date-separator">.</span>
          <select id="year" class="select-year">
            ${O(e)}
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
  `;const i=document.getElementById("allday"),r=document.getElementById("time-field");i.addEventListener("change",()=>{r.style.display=i.checked?"none":""}),t==null||t.BackButton.show(),t==null||t.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function p(){const f=document.getElementById("title").value.trim(),v=document.getElementById("day").value,d=document.getElementById("month").value,b=document.getElementById("year").value,s=document.getElementById("time").value,y=document.getElementById("allday").checked,u=document.getElementById("yearly").checked,B=document.getElementById("description").value.trim()||null,h=document.getElementById("place").value.trim()||null,E=document.getElementById("link").value.trim()||null,m=document.getElementById("error");if(!f){m.textContent="Заполните обязательные поля",m.classList.remove("hidden");return}const I=P(v,d,b,s,y,u);t==null||t.MainButton.showProgress(),t==null||t.MainButton.disable(),m.classList.add("hidden");try{await $("/api/events",{title:f,date:I,isYearly:u,description:B,place:h,link:E}),t==null||t.close()}catch(w){m.textContent=w instanceof Error?w.message:"Ошибка при сохранении",m.classList.remove("hidden"),t==null||t.MainButton.hideProgress(),t==null||t.MainButton.enable()}}t==null||t.MainButton.setText("Добавить событие"),t==null||t.MainButton.show(),t==null||t.MainButton.enable(),t==null||t.MainButton.onClick(p)}const C=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function A(){return C.map((n,o)=>`<option value="${o+1}">${n}</option>`).join("")}function D(){return Array.from({length:31},(n,o)=>`<option value="${o+1}">${o+1}</option>`).join("")}function F(n){var p;const o=new Date,a=o.getMonth()+1,l=o.getDate(),e=(p=window.Telegram)==null?void 0:p.WebApp;n.innerHTML=`
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
            ${D()}
          </select>
          <select id="month" class="select-month">
            ${A()}
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
  `;const t=document.getElementById("day"),i=document.getElementById("month");t.value=String(l),i.value=String(a),e==null||e.BackButton.show(),e==null||e.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function r(){const c=document.getElementById("name").value.trim(),f=document.getElementById("day").value,v=document.getElementById("month").value,d=document.getElementById("year").value.trim(),b=document.getElementById("contact").value.trim()||null,s=document.getElementById("error");if(!c){s.textContent="Заполните обязательные поля",s.classList.remove("hidden");return}const y=f.padStart(2,"0"),u=v.padStart(2,"0"),B=d?`${y}.${u}.${d}`:`${y}.${u}`;e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),s.classList.add("hidden");try{await $("/api/birthdays",{personName:c,date:B,birthYear:d||null,contact:b}),e==null||e.close()}catch(h){s.textContent=h instanceof Error?h.message:"Ошибка при сохранении",s.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}}e==null||e.MainButton.setText("Добавить день рождения"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(r)}function N(){return new URLSearchParams(window.location.search).get("form")??""}function g(){var l;const n=(l=window.Telegram)==null?void 0:l.WebApp;n==null||n.ready(),n==null||n.expand();const o=document.getElementById("app"),a=N();a==="event"?S(o):a==="birthday"?F(o):(n==null||n.BackButton.show(),n==null||n.BackButton.onClick(()=>n==null?void 0:n.close()),o.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `)}g();
