(function(){const n=document.createElement("link").relList;if(n&&n.supports&&n.supports("modulepreload"))return;for(const e of document.querySelectorAll('link[rel="modulepreload"]'))a(e);new MutationObserver(e=>{for(const i of e)if(i.type==="childList")for(const t of i.addedNodes)t.tagName==="LINK"&&t.rel==="modulepreload"&&a(t)}).observe(document,{childList:!0,subtree:!0});function l(e){const i={};return e.integrity&&(i.integrity=e.integrity),e.referrerPolicy&&(i.referrerPolicy=e.referrerPolicy),e.crossOrigin==="use-credentials"?i.credentials="include":e.crossOrigin==="anonymous"?i.credentials="omit":i.credentials="same-origin",i}function a(e){if(e.ep)return;e.ep=!0;const i=l(e);fetch(e.href,i)}})();const B="https://snakefrog.duckdns.org";function E(){var o,n;return((n=(o=window.Telegram)==null?void 0:o.WebApp)==null?void 0:n.initData)??""}async function b(o,n){const l=await fetch(`${B}${o}`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`tma ${E()}`},body:JSON.stringify(n)});if(!l.ok){const a=await l.text().catch(()=>l.statusText);throw new Error(a||`HTTP ${l.status}`)}}function w(o,n,l,a){if(a){const[,e,i]=o.split("-");return`${i}.${e}`}return l?o:`${o} ${n}`}function I(o){var i;const n=new Date().toISOString().split("T")[0];o.innerHTML=`
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
  `;const l=document.getElementById("allday"),a=document.getElementById("time-field");l.addEventListener("change",()=>{a.style.display=l.checked?"none":"flex"});const e=(i=window.Telegram)==null?void 0:i.WebApp;e==null||e.MainButton.setText("Добавить событие"),e==null||e.MainButton.show(),e==null||e.MainButton.enable(),e==null||e.MainButton.onClick(async()=>{const t=document.getElementById("title").value.trim(),s=document.getElementById("date").value,u=document.getElementById("time").value,y=document.getElementById("allday").checked,m=document.getElementById("yearly").checked,c=document.getElementById("description").value.trim()||null,h=document.getElementById("place").value.trim()||null,d=document.getElementById("link").value.trim()||null,r=document.getElementById("error");if(!t||!s){r.textContent="Заполните обязательные поля",r.classList.remove("hidden");return}const f=w(s,u,y,m);e==null||e.MainButton.showProgress(),e==null||e.MainButton.disable(),r.classList.add("hidden");try{await b("/api/events",{title:t,date:f,isYearly:m,description:c,place:h,link:d}),e==null||e.close()}catch(p){r.textContent=p instanceof Error?p.message:"Ошибка при сохранении",r.classList.remove("hidden"),e==null||e.MainButton.hideProgress(),e==null||e.MainButton.enable()}})}const M=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];function x(){return M.map((o,n)=>`<option value="${n+1}">${o}</option>`).join("")}function $(){return Array.from({length:31},(o,n)=>`<option value="${n+1}">${n+1}</option>`).join("")}function g(o){var s;const n=new Date,l=n.getMonth()+1,a=n.getDate();o.innerHTML=`
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
            ${$()}
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
  `;const e=document.getElementById("day"),i=document.getElementById("month");e.value=String(a),i.value=String(l);const t=(s=window.Telegram)==null?void 0:s.WebApp;t==null||t.MainButton.setText("Добавить день рождения"),t==null||t.MainButton.show(),t==null||t.MainButton.enable(),t==null||t.MainButton.onClick(async()=>{const u=document.getElementById("name").value.trim(),y=document.getElementById("day").value,m=document.getElementById("month").value,c=document.getElementById("year").value.trim(),h=document.getElementById("contact").value.trim()||null,d=document.getElementById("error");if(!u){d.textContent="Заполните обязательные поля",d.classList.remove("hidden");return}const r=y.padStart(2,"0"),f=m.padStart(2,"0"),p=c?`${r}.${f}.${c}`:`${r}.${f}`;t==null||t.MainButton.showProgress(),t==null||t.MainButton.disable(),d.classList.add("hidden");try{await b("/api/birthdays",{personName:u,date:p,birthYear:c||null,contact:h}),t==null||t.close()}catch(v){d.textContent=v instanceof Error?v.message:"Ошибка при сохранении",d.classList.remove("hidden"),t==null||t.MainButton.hideProgress(),t==null||t.MainButton.enable()}})}function L(){return new URLSearchParams(window.location.search).get("form")??""}function T(){var a;const o=(a=window.Telegram)==null?void 0:a.WebApp;o==null||o.ready(),o==null||o.expand();const n=document.getElementById("app"),l=L();l==="event"?I(n):l==="birthday"?g(n):n.innerHTML=`
      <div class="menu">
        <h2>SnakeFrog Calendar</h2>
        <a href="?form=event" class="btn">📅 Добавить событие</a>
        <a href="?form=birthday" class="btn">🎂 Добавить день рождения</a>
      </div>
    `}T();
