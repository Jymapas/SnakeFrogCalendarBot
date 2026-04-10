(function(){const n=document.createElement("link").relList;if(n&&n.supports&&n.supports("modulepreload"))return;for(const e of document.querySelectorAll('link[rel="modulepreload"]'))a(e);new MutationObserver(e=>{for(const i of e)if(i.type==="childList")for(const l of i.addedNodes)l.tagName==="LINK"&&l.rel==="modulepreload"&&a(l)}).observe(document,{childList:!0,subtree:!0});function t(e){const i={};return e.integrity&&(i.integrity=e.integrity),e.referrerPolicy&&(i.referrerPolicy=e.referrerPolicy),e.crossOrigin==="use-credentials"?i.credentials="include":e.crossOrigin==="anonymous"?i.credentials="omit":i.credentials="same-origin",i}function a(e){if(e.ep)return;e.ep=!0;const i=t(e);fetch(e.href,i)}})();const w="";function E(){var o,n;return((n=(o=window.Telegram)==null?void 0:o.WebApp)==null?void 0:n.initData)??""}async function B(o,n){const t=await fetch(`${w}${o}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${E()}`},body:JSON.stringify(n)});if(!t.ok){const a=await t.text().catch(()=>t.statusText);throw new Error(a||`HTTP ${t.status}`)}}function I(o,n,t,a){if(a){const[,e,i]=o.split("-");return`${i}.${e}`}return t?o:`${o} ${n}`}function M(o){var l;const n=new Date().toISOString().split("T")[0],t=(l=window.Telegram)==null?void 0:l.WebApp;o.innerHTML=`
    <h2 class="form-title">Новое событие</h2>
    <form id="event-form" novalidate>
      <div class="field">
        <label for="title">Название *</label>
        <input id="title" type="text" placeholder="Например: Встреча с врачом" required />
      </div>

      <div class="field">
        <label for="date">Дата *</label>
        <input id="date" type="date" value="${n}" required />
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
  `;const a=document.getElementById("allday"),e=document.getElementById("time-field");a.addEventListener("change",()=>{e.style.display=a.checked?"none":""}),t==null||t.BackButton.show(),t==null||t.BackButton.onClick(()=>history.back());async function i(){const u=document.getElementById("title").value.trim(),s=document.getElementById("date").value,m=document.getElementById("time").value,h=document.getElementById("allday").checked,f=document.getElementById("yearly").checked,c=document.getElementById("description").value.trim()||null,v=document.getElementById("place").value.trim()||null,d=document.getElementById("link").value.trim()||null,r=document.getElementById("error");if(!u||!s){r.textContent="Заполните обязательные поля",r.classList.remove("hidden");return}const p=I(s,m,h,f);t==null||t.MainButton.showProgress(),t==null||t.MainButton.disable(),r.classList.add("hidden");try{await B("/api/events",{title:u,date:p,isYearly:f,description:c,place:v,link:d}),t==null||t.close()}catch(y){r.textContent=y instanceof Error?y.message:"Ошибка при сохранении",r.classList.remove("hidden"),t==null||t.MainButton.hideProgress(),t==null||t.MainButton.enable()}}t==null||t.MainButton.setText("Добавить событие"),t==null||t.MainButton.show(),t==null||t.MainButton.enable(),t==null||t.MainButton.onClick(i)}const $=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function x(){return $.map((o,n)=>`<option value="${n+1}">${o}</option>`).join("")}function L(){return Array.from({length:31},(o,n)=>`<option value="${n+1}">${n+1}</option>`).join("")}function T(o){var s;const n=new Date,t=n.getMonth()+1,a=n.getDate(),e=(s=window.Telegram)==null?void 0:s.WebApp;o.innerHTML=`
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
            ${L()}
          </select>
          <select id="month" class="select-month">
            ${x()}
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
  `;const i=document.getElementById("day"),l=document.getElementById("month");i.value=String(a),l.value=String(t),e==null||e.BackButton.show(),e==null||e.BackButton.onClick(()=>history.back());async function u(){const m=document.getElementById("name").value.trim(),h=document.getElementById("day").value,f=document.getElementById("month").value,c=document.getElementById("year").value.trim(),v=document.getElementById("contact").value.trim()||null,d=document.getElementById("error");if(!m){d.textContent="Заполните обязательные поля",d.classList.remove("hidden");return}const r=h.padStart(2,"0"),p=f.padStart(2,"0"),y=c?`${r}.${p}.${c}`:`${r}.${p}`;e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),d.classList.add("hidden");try{await B("/api/birthdays",{personName:m,date:y,birthYear:c||null,contact:v}),e==null||e.close()}catch(b){d.textContent=b instanceof Error?b.message:"Ошибка при сохранении",d.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}}e==null||e.MainButton.setText("Добавить день рождения"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(u)}function k(){return new URLSearchParams(window.location.search).get("form")??""}function P(){var a;const o=(a=window.Telegram)==null?void 0:a.WebApp;o==null||o.ready(),o==null||o.expand();const n=document.getElementById("app"),t=k();t==="event"?M(n):t==="birthday"?T(n):n.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `}P();
