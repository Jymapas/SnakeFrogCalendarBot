(function(){const o=document.createElement("link").relList;if(o&&o.supports&&o.supports("modulepreload"))return;for(const e of document.querySelectorAll('link[rel="modulepreload"]'))a(e);new MutationObserver(e=>{for(const i of e)if(i.type==="childList")for(const r of i.addedNodes)r.tagName==="LINK"&&r.rel==="modulepreload"&&a(r)}).observe(document,{childList:!0,subtree:!0});function t(e){const i={};return e.integrity&&(i.integrity=e.integrity),e.referrerPolicy&&(i.referrerPolicy=e.referrerPolicy),e.crossOrigin==="use-credentials"?i.credentials="include":e.crossOrigin==="anonymous"?i.credentials="omit":i.credentials="same-origin",i}function a(e){if(e.ep)return;e.ep=!0;const i=t(e);fetch(e.href,i)}})();const I="https://calendarpi.tail900a1a.ts.net".replace(/\/$/,"");function M(){var n,o;return((o=(n=window.Telegram)==null?void 0:n.WebApp)==null?void 0:o.initData)??""}async function $(n,o){const t=M();if(!t)throw new Error("Открой приложение через кнопку в Telegram");const a=await fetch(`${I}${n}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${t}`},body:JSON.stringify(o)});if(!a.ok){const e=await a.text().catch(()=>a.statusText);throw new Error(e||`HTTP ${a.status}`)}}function w(n){if(!n)return"";const[o,t,a]=n.split("-");return`${a}.${t}.${o}`}function x(n,o,t,a,e){if(e){const[,c,s]=n.split("-");return`${s}.${c}`}if(a)return n;const i=o.padStart(2,"0"),r=t.padStart(2,"0");return`${n} ${i}:${r}`}function T(n){return Array.from({length:24},(o,t)=>{const a=String(t).padStart(2,"0");return`<option value="${t}"${t===n?" selected":""}>${a}</option>`}).join("")}function L(n){return Array.from({length:60},(o,t)=>{const a=String(t).padStart(2,"0");return`<option value="${t}"${t===n?" selected":""}>${a}</option>`}).join("")}function S(n){var s;const o=new Date().toISOString().split("T")[0],t=(s=window.Telegram)==null?void 0:s.WebApp;n.innerHTML=`
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
          <input id="date" type="date" value="${o}" />
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
            ${T(12)}
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
  `;const a=document.getElementById("date"),e=document.getElementById("date-display");a.addEventListener("input",()=>{e.textContent=w(a.value)});const i=document.getElementById("allday"),r=document.getElementById("time-field");i.addEventListener("change",()=>{r.style.display=i.checked?"none":""}),t==null||t.BackButton.show(),t==null||t.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function c(){const u=document.getElementById("title").value.trim(),p=document.getElementById("date").value,h=document.getElementById("hour").value,m=document.getElementById("minute").value,v=document.getElementById("allday").checked,l=document.getElementById("yearly").checked,f=document.getElementById("description").value.trim()||null,y=document.getElementById("place").value.trim()||null,b=document.getElementById("link").value.trim()||null,d=document.getElementById("error");if(!u||!p){d.textContent="Заполните обязательные поля",d.classList.remove("hidden");return}const E=x(p,h,m,v,l);t==null||t.MainButton.showProgress(),t==null||t.MainButton.disable(),d.classList.add("hidden");try{await $("/api/events",{title:u,date:E,isYearly:l,description:f,place:y,link:b}),t==null||t.close()}catch(B){d.textContent=B instanceof Error?B.message:"Ошибка при сохранении",d.classList.remove("hidden"),t==null||t.MainButton.hideProgress(),t==null||t.MainButton.enable()}}t==null||t.MainButton.setText("Добавить событие"),t==null||t.MainButton.show(),t==null||t.MainButton.enable(),t==null||t.MainButton.onClick(c)}const k=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function D(){return k.map((n,o)=>`<option value="${o+1}">${n}</option>`).join("")}function O(){return Array.from({length:31},(n,o)=>`<option value="${o+1}">${o+1}</option>`).join("")}function P(n){var s;const o=new Date,t=o.getMonth()+1,a=o.getDate(),e=(s=window.Telegram)==null?void 0:s.WebApp;n.innerHTML=`
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
            ${D()}
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
  `;const i=document.getElementById("day"),r=document.getElementById("month");i.value=String(a),r.value=String(t),e==null||e.BackButton.show(),e==null||e.BackButton.onClick(()=>{window.location.href=window.location.pathname});async function c(){const u=document.getElementById("name").value.trim(),p=document.getElementById("day").value,h=document.getElementById("month").value,m=document.getElementById("year").value.trim(),v=document.getElementById("contact").value.trim()||null,l=document.getElementById("error");if(!u){l.textContent="Заполните обязательные поля",l.classList.remove("hidden");return}const f=p.padStart(2,"0"),y=h.padStart(2,"0"),b=m?`${f}.${y}.${m}`:`${f}.${y}`;e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),l.classList.add("hidden");try{await $("/api/birthdays",{personName:u,date:b,birthYear:m||null,contact:v}),e==null||e.close()}catch(d){l.textContent=d instanceof Error?d.message:"Ошибка при сохранении",l.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}}e==null||e.MainButton.setText("Добавить день рождения"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(c)}function A(){return new URLSearchParams(window.location.search).get("form")??""}function C(){var a,e;const n=(a=window.Telegram)==null?void 0:a.WebApp;n==null||n.ready(),n==null||n.expand();const o=document.getElementById("app");if(!(n!=null&&n.initData)){o.innerHTML=`
      <div style="padding:16px;font-family:monospace;font-size:13px">
        <b style="color:red">⚠ initData пустой</b><br><br>
        Telegram defined: ${window.Telegram!==void 0}<br>
        WebApp defined: ${((e=window.Telegram)==null?void 0:e.WebApp)!==void 0}<br>
        platform: ${(n==null?void 0:n.platform)??"n/a"}<br>
        version: ${(n==null?void 0:n.version)??"n/a"}<br>
        initData: "${(n==null?void 0:n.initData)??"undefined"}"<br>
      </div>
    `;return}const t=A();t==="event"?S(o):t==="birthday"?P(o):(n==null||n.BackButton.show(),n==null||n.BackButton.onClick(()=>n==null?void 0:n.close()),o.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `)}C();
