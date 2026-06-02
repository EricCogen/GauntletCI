/* GauntletCI app mockup — navigation, personas, charts */
const NAV = {
  cto:{home:'p-home-cto',prominent:['home','trends','repos','audit'],all:['home','prs','repos','trends','rules','integrations','audit','settings']},
  em:{home:'p-home-em',prominent:['home','prs','repos','trends','audit'],all:['home','prs','repos','trends','rules','integrations','audit','settings']},
  tl:{home:'p-home-tl',prominent:['home','prs','repos','rules','trends'],all:['home','prs','repos','trends','rules','integrations','audit','settings']},
  dev:{home:'p-home-dev',prominent:['home','prs'],all:['home','prs','repos','trends','rules','integrations','audit','settings']},
  sec:{home:'p-home-sec',prominent:['home','audit','trends','settings'],all:['home','prs','repos','trends','rules','integrations','audit','settings']},
  ops:{home:'p-home-ops',prominent:['home','integrations','settings','audit'],all:['home','prs','repos','trends','rules','integrations','audit','settings']}
};
const ITEMS=[
  {id:'home',icon:'ti-home',label:'Home',page:null},
  {id:'prs',icon:'ti-git-pull-request',label:'Pull requests',page:'p-prs'},
  {id:'repos',icon:'ti-folder',label:'Repositories',page:'p-repos'},
  {id:'trends',icon:'ti-chart-bar',label:'Risk trends',page:'p-trends'},
  {id:'rules',icon:'ti-list-check',label:'Rules',page:'p-rules'},
  {id:'integrations',icon:'ti-plug',label:'Integrations',page:'p-integrations'},
  {id:'audit',icon:'ti-file-description',label:'Audit log',page:'p-audit'},
  {id:'settings',icon:'ti-settings',label:'Settings',page:'p-settings'}
];
let curRole='cto';
let curNav='home';

function buildNav(role){
  const cfg=NAV[role];
  const sec=document.getElementById('navSec');
  sec.innerHTML='';
  ITEMS.forEach(item=>{
    const el=document.createElement('div');
    const isActive=item.id===curNav;
    const isDim=!cfg.prominent.includes(item.id);
    el.className='nav-item'+(isActive?' active':'')+(isDim&&!isActive?' dim':'');
    el.innerHTML=`<i class="ti ${item.icon}" aria-hidden="true"></i>${item.label}`;
    el.onclick=()=>navTo(item.id,role);
    sec.appendChild(el);
  });
}

function navTo(navId,role){
  role=role||curRole;
  curNav=navId;
  const cfg=NAV[role];
  const targetPage=navId==='home'?cfg.home:ITEMS.find(i=>i.id===navId).page;
  document.querySelectorAll('.page').forEach(p=>p.classList.remove('active'));
  document.getElementById(targetPage).classList.add('active');
  buildNav(role);
}

function switchRole(role){
  curRole=role;
  curNav='home';
  const sel=document.getElementById('roleSelect');
  if(sel)sel.value=role;
  navTo('home',role);
}

function buildTrendChart(id){
  const data=[{p:93,w:21,b:6},{p:101,w:19,b:8},{p:117,w:14,b:5},{p:126,w:11,b:4}];
  const el=document.getElementById(id);
  if(!el)return;
  el.innerHTML='';
  const max=135;
  data.forEach(d=>{
    const g=document.createElement('div');
    g.className='tbar-g';
    const ph=Math.round(d.p/max*62),wh=Math.round(d.w/max*62),bh=Math.round(d.b/max*62);
    g.innerHTML=`<div class="tbar" style="height:${ph}px;background:#1D9E75"></div><div class="tbar" style="height:${wh}px;background:#EF9F27"></div><div class="tbar" style="height:${bh}px;background:#E24B4A"></div>`;
    el.appendChild(g);
  });
}

function buildAlignTrendChart(id,h=52){
  const data=[{f:88,p:9,d:2,u:1},{f:85,p:10,d:3,u:2},{f:82,p:11,d:4,u:3},{f:82,p:13,d:3,u:2}];
  const el=document.getElementById(id);
  if(!el)return;
  el.innerHTML='';
  const max=104;
  data.forEach(d=>{
    const g=document.createElement('div');
    g.className='tbar-g';
    const fh=Math.round(d.f/max*h),ph=Math.round(d.p/max*h),dh=Math.round(d.d/max*h),uh=Math.round(d.u/max*h);
    g.innerHTML=`<div class="tbar" style="height:${fh}px;background:#1D9E75"></div><div class="tbar" style="height:${ph}px;background:#EF9F27"></div><div class="tbar" style="height:${dh}px;background:#7F77DD"></div><div class="tbar" style="height:${uh}px;background:#C8C8C8"></div>`;
    el.appendChild(g);
  });
}

function buildSparkChart(id,values,color='#7F77DD',maxH=58){
  const el=document.getElementById(id);
  if(!el)return;
  el.innerHTML='';
  const max=Math.max(...values,1);
  values.forEach(v=>{
    const b=document.createElement('div');
    b.className='spark-bar';
    b.style.height=Math.round(v/max*maxH)+'px';
    b.style.background=color;
    el.appendChild(b);
  });
}

function buildHBarChart(id,rows){
  const el=document.getElementById(id);
  if(!el)return;
  el.innerHTML='';
  const max=Math.max(...rows.map(r=>r.value),1);
  rows.forEach(r=>{
    const row=document.createElement('div');
    row.className='hbar-row';
    const pct=Math.round(r.value/max*100);
    row.innerHTML=`<span class="hbar-lbl">${r.label}</span><div class="hbar-track"><div class="hbar-fill" style="width:${pct}%;background:${r.color||'#7F77DD'}"></div></div><span class="hbar-val">${r.display??r.value}</span>`;
    el.appendChild(row);
  });
}

function buildEmStatusChart(id){
  const data=[{h:1,w:0,e:0},{h:1,w:0,e:0},{h:1,w:1,e:0},{h:1,w:0,e:1}];
  const el=document.getElementById(id);
  if(!el)return;
  el.innerHTML='';
  const max=3;
  data.forEach(d=>{
    const g=document.createElement('div');
    g.className='tbar-g';
    const hh=Math.round(d.h/max*44),wh=Math.round(d.w/max*44),eh=Math.round(d.e/max*44);
    g.innerHTML=`<div class="tbar" style="height:${hh}px;background:#1D9E75"></div><div class="tbar" style="height:${wh}px;background:#EF9F27"></div><div class="tbar" style="height:${eh}px;background:#E24B4A"></div>`;
    el.appendChild(g);
  });
}

buildNav('cto');
buildTrendChart('ctoTrend');
buildTrendChart('trendsChart');
buildAlignTrendChart('alignTrend',52);
buildSparkChart('ctoRiskSpark',[28,32,30,35,38,40,39,42],'#1D9E75');
buildSparkChart('ctoBlockedSpark',[14,12,11,10,9,8,9,8],'#E24B4A');
buildSparkChart('ctoPolicySpark',[89,90,91,92,93,93,94,94],'#534AB7');
buildHBarChart('ctoOrgHealth',[
  {label:'Payments',value:62,display:'62',color:'#E24B4A'},
  {label:'Platform',value:91,display:'91',color:'#1D9E75'},
  {label:'Data',value:77,display:'77',color:'#EF9F27'}
]);
buildHBarChart('ctoRiskDomain',[
  {label:'Data access',value:34,display:'34',color:'#7F77DD'},
  {label:'Async',value:26,display:'26',color:'#7F77DD'},
  {label:'Retry',value:19,display:'19',color:'#7F77DD'},
  {label:'Other',value:21,display:'21',color:'#B0B0B0'}
]);
buildHBarChart('ctoGovernance',[
  {label:'Policy enforced',value:94,display:'94%',color:'#1D9E75'},
  {label:'Audit ready',value:100,display:'100%',color:'#1D9E75'},
  {label:'Risk acceptance',value:72,display:'7 open',color:'#EF9F27'}
]);
buildEmStatusChart('ctoEmStatus');
buildHBarChart('emTeamHealth',[
  {label:'Payments',value:62,display:'62',color:'#E24B4A'},
  {label:'Platform',value:91,display:'91',color:'#1D9E75'},
  {label:'Data',value:77,display:'77',color:'#EF9F27'}
]);
buildHBarChart('emRiskPatterns',[
  {label:'EF Core / data',value:34,display:'+22%',color:'#E24B4A'},
  {label:'Retry drift',value:19,display:'6 PRs',color:'#EF9F27'},
  {label:'Async blocking',value:14,display:'4 PRs',color:'#EF9F27'}
]);

window.switchRole = switchRole;

/* MCP host / visualize widget runtime */
"use strict";
(() => {
  var __create = Object.create;
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __getProtoOf = Object.getPrototypeOf;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __require = /* @__PURE__ */ ((x) => typeof require !== "undefined" ? require : typeof Proxy !== "undefined" ? new Proxy(x, {
    get: (a, b) => (typeof require !== "undefined" ? require : a)[b]
  }) : x)(function(x) {
    if (typeof require !== "undefined") return require.apply(this, arguments);
    throw Error('Dynamic require of "' + x + '" is not supported');
  });
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
    // If the importer is in node compatibility mode or this is not an ESM
    // file that has been converted to a CommonJS file using a Babel-
    // compatible transform (i.e. "__esModule" has not been set), then set
    // "default" to the CommonJS "module.exports" for node compatibility.
    isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
    mod
  ));

  // src/scripts/mcp-app-helper.ts
  var app2 = /* @__PURE__ */ (() => {
    let nextId = 100;
    return {
      sendRequest({ method, params }) {
        const id = nextId++;
        window.parent.postMessage({ jsonrpc: "2.0", id, method, params }, "*");
        return new Promise((resolve, reject) => {
          window.addEventListener(
            "message",
            function listener(event) {
              if (event.data?.id === id) {
                window.removeEventListener("message", listener);
                if (event.data?.result) {
                  resolve(event.data.result);
                } else if (event.data?.error) {
                  const e = event.data.error;
                  reject(
                    new Error(
                      typeof e === "string" ? e : e.message || JSON.stringify(e)
                    )
                  );
                }
              }
            }
          );
        });
      },
      sendNotification({ method, params }) {
        window.parent.postMessage({ jsonrpc: "2.0", method, params }, "*");
      },
      /**
       * Attach files to the conversation — structured clone carries File objects
       * through postMessage (web-only, mobile relay JSON.stringifies).
       * Uses a non-JSON-RPC shape so PostMessageTransport drops it, raw listener
       * in AppRenderer catches it. Requires real user click (userActivation gate
       * enforced host-side per PR #31090).
       */
      attachFiles(files) {
        window.parent.postMessage({ type: "mcp-host:attach-files", files }, "*");
      },
      onNotification(method, handler) {
        window.addEventListener(
          "message",
          function listener(event) {
            if (event.data?.method === method) {
              handler(event.data.params);
            }
          }
        );
      },
      async requestDisplayMode({ mode }) {
        return this.sendRequest({
          method: "ui/request-display-mode",
          params: { mode }
        });
      },
      setupAutoResize() {
        new ResizeObserver(() => {
          const { body, documentElement: html } = document;
          const htmlStyle = getComputedStyle(html);
          const rect = body.getBoundingClientRect();
          const width = Math.ceil(rect.width);
          const height = Math.ceil(
            rect.height + (parseFloat(htmlStyle.borderTop) || 0) + (parseFloat(htmlStyle.borderBottom) || 0)
          );
          this.sendNotification({
            method: "ui/notifications/size-changed",
            params: { width, height }
          });
        }).observe(document.body);
      }
    };
  })();
  window.app = app2;

  // src/scripts/streaming-morph.ts
  function createStreamingRenderer2(options = {}) {
    const {
      stripScripts = false,
      widgetName = "Widget",
      shouldRender = () => true,
      onFirstRender
    } = options;
    let didFirstRender = false;
    const markFirstRender2 = () => {
      if (!didFirstRender) {
        didFirstRender = true;
        onFirstRender?.();
      }
    };
    let parserModules = null;
    let morphdomModule = null;
    let streamingParser = null;
    let prevCode = "";
    async function renderPartial(container2, code) {
      if (!code) return;
      if (!parserModules || !morphdomModule) {
        try {
          const [htmlparser2, domhandler, domSerializer, morphdom] = await Promise.all([
            import("https://esm.sh/htmlparser2@9.1.0"),
            import("https://esm.sh/domhandler@5.0.3"),
            import("https://esm.sh/dom-serializer@2.0.0"),
            import("https://esm.sh/morphdom@2.7.4")
          ]);
          parserModules = {
            Parser: htmlparser2.Parser,
            DomHandler: domhandler.DomHandler,
            render: domSerializer.default
          };
          morphdomModule = morphdom.default;
        } catch (err) {
          console.warn(
            `[${widgetName}] CDN failed, falling back to innerHTML:`,
            err
          );
          container2.innerHTML = code;
          markFirstRender2();
          return;
        }
      }
      if (code.length < prevCode.length || !streamingParser) {
        const handler = new parserModules.DomHandler();
        streamingParser = {
          parser: new parserModules.Parser(handler, {
            decodeEntities: false,
            lowerCaseAttributeNames: false,
            lowerCaseTags: false,
            recognizeSelfClosing: true
          }),
          handler,
          write(chunk) {
            this.parser.write(chunk);
          },
          serialize() {
            return parserModules.render(this.handler.root.children, {
              encodeEntities: false
            });
          }
        };
        prevCode = "";
      }
      const newChunk = code.substring(prevCode.length);
      if (newChunk) {
        streamingParser.write(newChunk);
        prevCode = code;
      }
      let serialized = streamingParser.serialize();
      if (serialized) {
        if (stripScripts) {
          serialized = serialized.replace(/<script[\s\S]*?<\/script>/gi, "");
        }
        if (shouldRender(serialized)) {
          const tempContainer = container2.cloneNode(false);
          tempContainer.innerHTML = serialized;
          morphdomModule(container2, tempContainer, { childrenOnly: true });
          markFirstRender2();
        }
      }
    }
    return { renderPartial };
  }
  window.createStreamingRenderer = createStreamingRenderer2;

  // src/scripts/clipboard-copy.ts
  var htmlToImage = null;
  async function loadHtmlToImage() {
    if (!htmlToImage) {
      htmlToImage = await import("https://esm.sh/html-to-image@1.11.11");
    }
    return htmlToImage;
  }
  async function copyToClipboard2(_container) {
    const lib = await loadHtmlToImage();
    const target = document.body;
    const width = target.scrollWidth || 700;
    const height = target.scrollHeight || 400;
    const blob = await lib.toBlob(target, {
      pixelRatio: 2,
      width,
      height,
      filter: (node) => !(node instanceof HTMLElement && (node.id === "action-btns" || node.id === "copy-toast"))
    });
    if (!blob) throw new Error("Failed to create PNG");
    const isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
    if (isSafari) {
      await navigator.clipboard.write([
        new ClipboardItem({ "image/png": Promise.resolve(blob) })
      ]);
    } else {
      await navigator.clipboard.write([new ClipboardItem({ "image/png": blob })]);
    }
  }
  window.copyToClipboard = copyToClipboard2;

  // src/scripts/host-init.ts
  function initHostContext2(result) {
    const hostContext = result.hostContext;
    if (!hostContext) return;
    const styles = hostContext.styles;
    if (!styles) return;
    const css = styles.css;
    if (css?.fonts) {
      const fontStyle = document.getElementById("mcp-host-fonts");
      if (fontStyle) {
        fontStyle.textContent = css.fonts;
      }
    }
    const variables = styles.variables;
    if (variables) {
      const style = document.createElement("style");
      style.id = "mcp-host-variables";
      style.textContent = ":root {\n" + Object.entries(variables).map(([key, value]) => "  " + key + ": " + value + ";").join("\n") + "\n}";
      document.head.appendChild(style);
    }
    applyHostStrings(hostContext._hostStrings);
  }
  function applyHostStrings(hostStrings) {
    if (!hostStrings || typeof hostStrings !== "object") return;
    const strings = hostStrings;
    document.querySelectorAll("[data-i18n]").forEach((el) => {
      const key = el.dataset.i18n;
      const val = key ? strings[key] : void 0;
      if (val && val.trim()) el.textContent = val;
    });
    window.__hostStrings = strings;
  }
  window.addEventListener("message", (event) => {
    const data = event.data;
    if (data?.method === "ui/notifications/host-context-changed") {
      applyHostStrings(data.params?._hostStrings);
    }
  });
  window.initHostContext = initHostContext2;

  // src/scripts/send-prompt.ts
  window.sendPrompt = function sendPrompt(text) {
    app.sendRequest({
      method: "ui/message",
      params: { role: "user", content: [{ type: "text", text }] }
    });
  };

  // src/scripts/elicitation.ts
  var selectedFiles = /* @__PURE__ */ new Map();
  var submittedKey;
  var FADE_MS = 150;
  function cssStringEscape(s) {
    return s.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
  }
  function fingerprint(s) {
    let h = 5381;
    for (let i = 0; i < s.length; i++) h = (h << 5) + h + s.charCodeAt(i) | 0;
    return `elicit-submitted:${s.length}:${h.toString(36)}`;
  }
  function escapeHtml(s) {
    const map = {
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;"
    };
    return s.replace(/[&<>"']/g, (c) => map[c]);
  }
  function renderFileTile(container2, name, file) {
    const tile = document.createElement("div");
    tile.className = "elicit-file-tile";
    const dot = file.name.lastIndexOf(".");
    const ext = dot > 0 && file.name.length - dot <= 6 ? file.name.slice(dot + 1).toUpperCase() : "FILE";
    tile.innerHTML = `
    <button type="button" class="remove" aria-label="Remove ${escapeHtml(file.name)}">\xD7</button>
    <div class="name">${escapeHtml(file.name)}</div>
    <div class="badge">${escapeHtml(ext)}</div>
  `;
    tile.querySelector(".remove")?.addEventListener("click", () => {
      const list = selectedFiles.get(name);
      if (list) {
        const idx = list.indexOf(file);
        if (idx >= 0) list.splice(idx, 1);
      }
      tile.remove();
    });
    container2.appendChild(tile);
  }
  function renderSubmittedSummary(form, answers, skipped) {
    const summary = document.createElement("div");
    summary.className = "elicit-summary";
    const appendRow = (label, parts) => {
      const row = document.createElement("div");
      row.className = "elicit-summary-row";
      if (label !== void 0) {
        const q = document.createElement("span");
        q.className = "elicit-summary-q";
        q.textContent = label;
        row.appendChild(q);
      }
      for (const part of parts) {
        const a = document.createElement("span");
        a.className = "elicit-summary-a";
        a.textContent = part;
        row.appendChild(a);
      }
      summary.appendChild(row);
    };
    const nonEmpty = (v) => {
      if (v === void 0 || v === "") return void 0;
      if (Array.isArray(v)) return v.length ? v.join(", ") : void 0;
      return v;
    };
    if (skipped) {
      appendRow(void 0, ["Skipped \u2014 proceeding with defaults."]);
    } else {
      const seen = /* @__PURE__ */ new Set();
      form.querySelectorAll(".elicit-group").forEach((group) => {
        const label = group.querySelector(".elicit-question")?.textContent?.trim();
        const parts = [];
        group.querySelectorAll("[data-name]").forEach((el) => {
          const name = el.dataset.name;
          if (!name || seen.has(name)) return;
          seen.add(name);
          const v = nonEmpty(answers[name]);
          if (v !== void 0) parts.push(v);
          const other = nonEmpty(answers[`${name}_other`]);
          if (other !== void 0) {
            seen.add(`${name}_other`);
            parts.push(other);
          }
        });
        if (parts.length) appendRow(label, parts);
      });
      for (const [name, value] of Object.entries(answers)) {
        if (seen.has(name)) continue;
        const v = nonEmpty(value);
        if (v !== void 0) appendRow(name, [v]);
      }
      if (summary.childElementCount === 0) {
        appendRow(void 0, ["Submitted with defaults."]);
      }
    }
    form.querySelector(".elicit-body")?.replaceChildren(summary);
  }
  function wireElicitation() {
    const form = document.querySelector("form.elicit");
    if (!form) return;
    selectedFiles.clear();
    let submitted = false;
    form.addEventListener("submit", (e) => e.preventDefault());
    submittedKey = fingerprint(form.innerHTML);
    try {
      const stored = sessionStorage.getItem(submittedKey);
      if (stored) {
        form.classList.add("elicit-submitted", "elicit-restored");
        const parsed = JSON.parse(stored);
        if (parsed && typeof parsed === "object" && "answers" in parsed && "skipped" in parsed) {
          renderSubmittedSummary(
            form,
            parsed.answers,
            parsed.skipped
          );
        }
        return;
      }
    } catch {
    }
    form.querySelectorAll(".elicit-pills").forEach((container2) => {
      const isMulti = container2.dataset.multi === "true";
      const name = container2.dataset.name;
      container2.querySelectorAll(".elicit-pill").forEach((p) => p.setAttribute("aria-pressed", "false"));
      container2.addEventListener("click", (e) => {
        const pill = e.target.closest(
          ".elicit-pill"
        );
        if (!pill) return;
        if (!isMulti) {
          container2.querySelectorAll(".elicit-pill[aria-pressed]").forEach((p) => {
            if (p !== pill) p.setAttribute("aria-pressed", "false");
          });
        }
        const nowSelected = pill.getAttribute("aria-pressed") !== "true";
        pill.setAttribute("aria-pressed", String(nowSelected));
        if (name) {
          const otherInput = form.querySelector(
            `.elicit-other[data-for="${cssStringEscape(name)}"]`
          );
          if (otherInput) {
            const otherPillSelected = container2.querySelector(
              '.elicit-pill[data-other][aria-pressed="true"]'
            ) !== null;
            otherInput.hidden = !otherPillSelected;
            if (otherPillSelected) {
              otherInput.focus();
            }
          }
        }
      });
    });
    form.querySelectorAll(".elicit-files[data-name]").forEach((container2) => {
      const name = container2.dataset.name;
      selectedFiles.set(name, []);
      const dropzone = container2.querySelector(".elicit-dropzone");
      const input = dropzone?.querySelector("input[type='file']");
      if (!dropzone || !input) return;
      if (dropzone.tagName !== "LABEL") {
        dropzone.addEventListener("click", (e) => {
          if (e.target === input) return;
          input.click();
        });
      }
      input.addEventListener("change", () => {
        const files = Array.from(input.files ?? []);
        for (const file of files) {
          const entry = {
            name: file.name,
            size: file.size,
            type: file.type,
            raw: file
          };
          selectedFiles.get(name).push(entry);
          renderFileTile(container2, name, entry);
        }
        input.value = "";
      });
    });
    form.querySelector(".elicit-submit")?.addEventListener("click", () => {
      if (submitted) return;
      submitted = true;
      window.submitElicitation(collectElicitAnswers());
    });
    form.querySelector(".elicit-skip")?.addEventListener("click", () => {
      if (submitted) return;
      submitted = true;
      window.submitElicitation({}, { skipped: true });
    });
  }
  function collectElicitAnswers() {
    const form = document.querySelector("form.elicit");
    if (!form) return {};
    const answers = {};
    form.querySelectorAll(".elicit-pills[data-name]").forEach((container2) => {
      const name = container2.dataset.name;
      const isMulti = container2.dataset.multi === "true";
      const selected = Array.from(
        container2.querySelectorAll(
          ".elicit-pill[aria-pressed='true']"
        )
      ).map((p) => p.dataset.value ?? p.textContent?.trim() ?? "");
      answers[name] = isMulti ? selected : selected[0] ?? "";
      const otherInput = form.querySelector(
        `.elicit-other[data-for="${cssStringEscape(name)}"]`
      );
      if (otherInput && !otherInput.hidden && otherInput.value.trim()) {
        answers[`${name}_other`] = otherInput.value.trim();
      }
    });
    for (const [name, files] of selectedFiles) {
      if (files.length === 0) continue;
      answers[name] = files.map((f) => `${f.name} (attached)`);
    }
    form.querySelectorAll(
      "[data-name]:not(.elicit-pills):not(.elicit-other):not(.elicit-files)"
    ).forEach((el) => {
      const name = el.dataset.name;
      if (name && el.value.trim()) {
        answers[name] = el.value.trim();
      }
    });
    return answers;
  }
  window.submitElicitation = function submitElicitation(answers, options = {}) {
    const skipped = options.skipped ?? false;
    const form = document.querySelector("form.elicit");
    const title = form?.querySelector(".elicit-header > span")?.textContent?.trim();
    if (form) {
      form.classList.add("elicit-transitioning");
      const swap = () => {
        if (!form.isConnected) return;
        form.classList.add("elicit-submitted");
        renderSubmittedSummary(form, answers, skipped);
        form.classList.remove("elicit-transitioning");
      };
      setTimeout(swap, FADE_MS);
    }
    const persistSubmitted = () => {
      if (!submittedKey) return;
      try {
        sessionStorage.setItem(
          submittedKey,
          JSON.stringify({ answers, skipped })
        );
      } catch {
        try {
          sessionStorage.setItem(submittedKey, "1");
        } catch {
        }
      }
    };
    if (skipped) {
      window.sendPrompt(
        "(Skipped the form \u2014 proceed with defaults or ask me in plain text)"
      );
      persistSubmitted();
      return;
    }
    const rawFiles = [];
    for (const list of selectedFiles.values()) {
      for (const f of list) rawFiles.push(f.raw);
    }
    const humanizeKey = (k) => {
      let suffix = "";
      if (k.endsWith("_other")) {
        k = k.slice(0, -"_other".length);
        suffix = " (other)";
      } else if (k.endsWith("_file")) {
        k = k.slice(0, -"_file".length);
        suffix = " file";
      } else if (k.endsWith("_text")) {
        k = k.slice(0, -"_text".length);
      }
      const words = k.replace(/_/g, " ");
      return words.charAt(0).toUpperCase() + words.slice(1) + suffix;
    };
    const FOLD_AT = 200;
    const prefix = title ? `${title} \u2014 ` : "";
    const pairs = [];
    const folds = [];
    for (const [k, v] of Object.entries(answers)) {
      if (v === "" || Array.isArray(v) && v.length === 0) continue;
      const raw = Array.isArray(v) ? v.join(", ") : v;
      const label = humanizeKey(k);
      if (raw.length > FOLD_AT) {
        pairs.push(`${label}: (${raw.length} chars \u2014 see below)`);
        folds.push(`[${label}]
${raw}`);
      } else {
        const flat = raw.replace(/\r?\n/g, " / ");
        const value = flat.length > 80 ? `"${flat}"` : flat;
        pairs.push(`${label}: ${value}`);
      }
    }
    let payload = pairs.length === 0 ? `${prefix}proceeding with defaults.` : `${prefix}${pairs.join(" \xB7 ")}`;
    if (folds.length > 0) {
      payload += `

--- Full content ---
${folds.join("\n\n")}`;
    }
    if (rawFiles.length > 0) {
      window.parent.postMessage(
        { type: "mcp-host:elicit-submit", text: payload, files: rawFiles },
        "*"
      );
    } else {
      window.sendPrompt(payload);
      persistSubmitted();
    }
  };
  window.collectElicitAnswers = collectElicitAnswers;
  window._wireElicitation = wireElicitation;

  // src/scripts/open-link.ts
  window.openLink = function openLink(url) {
    return app.sendRequest({
      method: "ui/open-link",
      params: { url }
    });
  };
  document.getElementById("vis-container").addEventListener("click", (e) => {
    const anchor = e.target?.closest?.("a[href]");
    if (!anchor) return;
    const href = anchor.getAttribute("href");
    if (!href) return;
    if (href.startsWith("#") || href.startsWith("javascript:")) return;
    let url;
    try {
      url = new URL(href, window.location.href).href;
    } catch {
      return;
    }
    if (!/^https?:/.test(url)) return;
    e.preventDefault();
    window.openLink(url);
  });

  // src/scripts/svg-text-occlusion.ts
  function fixSvgTextOcclusion2(container2) {
    try {
      const svg = container2.querySelector("svg");
      if (!svg) return;
      const NS = "http://www.w3.org/2000/svg";
      const PAD_X = 4;
      const PAD_Y = 2;
      const MIN_CONNECTOR_LEN = 15;
      const textRects = [];
      svg.querySelectorAll("text").forEach((t) => {
        if (t.closest("defs, mask, clipPath, marker")) return;
        try {
          const bb = t.getBBox();
          if (bb.width < 1 || bb.height < 1) return;
          textRects.push({
            x: bb.x - PAD_X,
            y: bb.y - PAD_Y,
            w: bb.width + 2 * PAD_X,
            h: bb.height + 2 * PAD_Y
          });
        } catch {
        }
      });
      if (!textRects.length) return;
      const candidates = [];
      svg.querySelectorAll("line, path, polyline").forEach((el) => {
        if (el.closest("defs, mask, clipPath, marker")) return;
        if (el.tagName.toLowerCase() === "path") {
          const fill = el.getAttribute("fill") || getComputedStyle(el).fill;
          if (fill && fill !== "none" && fill !== "transparent" && !/^rgba\([^)]*,\s*0\)$/.test(fill))
            return;
        }
        const geom = el;
        try {
          const len = geom.getTotalLength();
          if (len < MIN_CONNECTOR_LEN) return;
          candidates.push({ el: geom, len });
        } catch {
        }
      });
      if (!candidates.length) return;
      const inRect = (px, py, r) => px >= r.x && px <= r.x + r.w && py >= r.y && py <= r.y + r.h;
      const intersecting = [];
      for (const { el, len } of candidates) {
        const step = Math.max(2, len / 80);
        let hit = false;
        for (let d = 0; d <= len && !hit; d += step) {
          const pt = el.getPointAtLength(d);
          for (const r of textRects) {
            if (inRect(pt.x, pt.y, r)) {
              hit = true;
              break;
            }
          }
        }
        if (hit) intersecting.push(el);
      }
      if (!intersecting.length) return;
      let defs = svg.querySelector(":scope > defs");
      if (!defs) {
        defs = document.createElementNS(NS, "defs");
        svg.insertBefore(defs, svg.firstChild);
      }
      const maskId = `imagine-text-gaps-${Math.random().toString(36).slice(2, 8)}`;
      const mask = document.createElementNS(NS, "mask");
      mask.setAttribute("id", maskId);
      mask.setAttribute("maskUnits", "userSpaceOnUse");
      const vb = (svg.getAttribute("viewBox") || "").trim().split(/[\s,]+/).map(Number);
      const [vx, vy, vw, vh] = vb.length === 4 && vb.every((n) => Number.isFinite(n)) ? vb : (() => {
        const bb = svg.getBBox();
        return [bb.x, bb.y, bb.width, bb.height];
      })();
      const bg = document.createElementNS(NS, "rect");
      bg.setAttribute("x", String(vx));
      bg.setAttribute("y", String(vy));
      bg.setAttribute("width", String(vw));
      bg.setAttribute("height", String(vh));
      bg.setAttribute("fill", "white");
      mask.appendChild(bg);
      for (const r of textRects) {
        const hole = document.createElementNS(NS, "rect");
        hole.setAttribute("x", String(r.x));
        hole.setAttribute("y", String(r.y));
        hole.setAttribute("width", String(r.w));
        hole.setAttribute("height", String(r.h));
        hole.setAttribute("fill", "black");
        hole.setAttribute("rx", "2");
        mask.appendChild(hole);
      }
      defs.appendChild(mask);
      for (const el of intersecting) {
        el.setAttribute("mask", `url(#${maskId})`);
      }
    } catch {
    }
  }
  window.fixSvgTextOcclusion = fixSvgTextOcclusion2;

  // src/scripts/svg-clip-fix.ts
  function fixSvgClipping2(container2) {
    try {
      const svg = container2.querySelector("svg");
      if (!svg) return;
      const vb = svg.getAttribute("viewBox");
      if (!vb) return;
      const [vbX, vbY, vbW, vbH] = vb.split(/[\s,]+/).map(Number);
      if (![vbX, vbY, vbW, vbH].every(Number.isFinite)) return;
      if (vbW < 100 || vbH < 100) return;
      let maxX = -Infinity;
      let maxY = -Infinity;
      svg.querySelectorAll(
        ":scope > :not(defs):not(style):not(title):not(desc):not(metadata)"
      ).forEach((el) => {
        try {
          const bb = el.getBBox();
          if (bb.width < 0.5 && bb.height < 0.5) return;
          maxX = Math.max(maxX, bb.x + bb.width);
          maxY = Math.max(maxY, bb.y + bb.height);
        } catch {
        }
      });
      const PAD = 10;
      const vbRight = vbX + vbW;
      const vbBottom = vbY + vbH;
      let newW = vbW;
      if (maxX !== -Infinity && maxX + PAD > vbRight + 1) {
        newW = maxX + PAD - vbX;
      }
      let newH = vbH;
      if (maxY !== -Infinity && maxY + PAD > vbBottom + 1) {
        newH = maxY + PAD - vbY;
      }
      if (newW !== vbW || newH !== vbH) {
        svg.setAttribute(
          "viewBox",
          `${vbX} ${vbY} ${round(newW)} ${round(newH)}`
        );
        const hAttr = svg.getAttribute("height");
        if (hAttr && newH !== vbH) {
          const px = parseFloat(hAttr);
          if (Number.isFinite(px)) {
            svg.setAttribute("height", String(round(px * (newH / vbH))));
          }
        }
      }
      svg.style.maxWidth = `${round(newW)}px`;
      svg.style.display = "block";
      svg.style.marginInline = "auto";
    } catch {
    }
  }
  function round(n) {
    return Math.round(n * 100) / 100;
  }
  globalThis.fixSvgClipping = fixSvgClipping2;

  // src/scripts/svg-text-edge-fix.ts
  function fixSvgTextEdgeClip2(container2) {
    try {
      const svg = container2.querySelector("svg");
      if (!svg) return;
      const vb = svg.getAttribute("viewBox");
      if (!vb) return;
      const [vbX, , vbW] = vb.split(/[\s,]+/).map(Number);
      if (!Number.isFinite(vbX) || !Number.isFinite(vbW)) return;
      const vbRight = vbX + vbW;
      const byKey = /* @__PURE__ */ new Map();
      svg.querySelectorAll("text").forEach((text) => {
        if (text.closest("svg") !== svg) return;
        if (text.closest("g[transform]")) return;
        if (text.hasAttribute("transform")) return;
        if (text.querySelector("tspan[x]")) return;
        const p = text.parentElement;
        if (p && p !== svg) {
          const hasBox = Array.from(
            p.querySelectorAll(
              ":scope > rect, :scope > circle, :scope > ellipse"
            )
          ).some((shape) => {
            try {
              return shape.getBBox().width > 10;
            } catch {
              return true;
            }
          });
          if (hasBox) return;
        }
        const anchor = text.getAttribute("text-anchor") || getComputedStyle(text).textAnchor || "start";
        if (anchor !== "start" && anchor !== "end") return;
        let bb;
        try {
          bb = text.getBBox();
        } catch {
          return;
        }
        if (bb.width < 1) return;
        const xAttr = text.getAttribute("x");
        if (!xAttr || /[\s,]/.test(xAttr)) return;
        const x = parseFloat(xAttr);
        if (!Number.isFinite(x)) return;
        const overflow = anchor === "start" ? bb.x + bb.width - vbRight : vbX - bb.x;
        const key = `${anchor}:${x}`;
        const group = byKey.get(key) ?? [];
        group.push({ text, x, anchor, overflow, width: bb.width });
        byKey.set(key, group);
      });
      byKey.forEach((group) => {
        const overflowing = group.filter((g) => g.overflow > 2);
        if (!overflowing.length) return;
        const worst = overflowing.reduce(
          (a, b) => a.overflow > b.overflow ? a : b
        );
        if (worst.overflow > worst.width * 0.5) return;
        const shift = group[0].anchor === "start" ? -worst.overflow : worst.overflow;
        group.forEach(
          ({ text, x }) => text.setAttribute("x", String(round2(x + shift)))
        );
      });
    } catch {
    }
  }
  function round2(n) {
    return Math.round(n * 100) / 100;
  }
  globalThis.fixSvgTextEdgeClip = fixSvgTextEdgeClip2;

  // src/scripts/widget-main.ts
  var IMPLEMENTATION = { name: "visualize widget", version: "1.0.0" };
  var container = document.getElementById("vis-container");
  var isSvgCode = (code) => code.trimStart().startsWith("<svg");
  var hasSvgVisuals = (code) => code && /<(rect|circle|ellipse|line|polyline|polygon|path|text|image|use|g|foreignObject)[\s>]/i.test(
    code.replace(/<style[\s\S]*?<\/style>/gi, "").replace(/<defs[\s\S]*?<\/defs>/gi, "")
  );
  var hasHtmlVisuals = (code) => code && code.replace(/<style[\s\S]*?<\/style>/gi, "").replace(/<script[\s\S]*?<\/script>/gi, "").replace(/<!--[\s\S]*?-->/g, "").trim().length > 0;
  var executeScripts = async () => {
    const scripts = Array.from(container.querySelectorAll("script"));
    for (const oldScript of scripts) {
      const newScript = document.createElement("script");
      Array.from(oldScript.attributes).forEach(
        (attr) => newScript.setAttribute(attr.name, attr.value)
      );
      newScript.textContent = oldScript.textContent;
      const hasSrc = newScript.hasAttribute("src");
      const loaded = hasSrc ? new Promise((resolve) => {
        newScript.onload = () => resolve();
        newScript.onerror = () => resolve();
      }) : Promise.resolve();
      oldScript.parentNode?.replaceChild(newScript, oldScript);
      await loaded;
    }
  };
  var vizTitle = "visualize";
  var iframeStartTime = performance.now();
  var firstRenderSent = false;
  var sendTiming = (event) => app.sendNotification({
    method: "notifications/message",
    params: {
      level: "info",
      logger: `viz:timing:${event}`,
      data: { iframe_ms: Math.round(performance.now() - iframeStartTime) }
    }
  });
  var sendAction = (action) => app.sendNotification({
    method: "notifications/message",
    params: { level: "info", logger: `viz:action:${action}`, data: { action } }
  });
  var markFirstRender = () => {
    if (!firstRenderSent) {
      firstRenderSent = true;
      sendTiming("firstrender");
    }
  };
  var svgRenderer = createStreamingRenderer({
    widgetName: "VisualizeWidget-SVG",
    shouldRender: hasSvgVisuals,
    onFirstRender: markFirstRender
  });
  var htmlRenderer = createStreamingRenderer({
    widgetName: "VisualizeWidget-HTML",
    stripScripts: true,
    shouldRender: hasHtmlVisuals,
    onFirstRender: markFirstRender
  });
  var renderFinal = async (code) => {
    container.classList.remove("streaming");
    if (isSvgCode(code)) {
      if (hasSvgVisuals(code)) {
        container.innerHTML = code;
        markFirstRender();
        fixSvgTextEdgeClip(container);
        fixSvgClipping(container);
        fixSvgTextOcclusion(container);
      }
    } else {
      if (hasHtmlVisuals(code)) {
        container.innerHTML = code;
        markFirstRender();
        await executeScripts();
        container.classList.remove("scripts-loading");
        window._wireElicitation();
        fixSvgTextEdgeClip(container);
        fixSvgClipping(container);
        fixSvgTextOcclusion(container);
      }
    }
    window.dispatchEvent(
      new CustomEvent("viz:complete", {
        detail: { code, title: vizTitle }
      })
    );
    sendTiming("complete");
  };
  var renderPartialCode = (code) => {
    container.classList.add("streaming");
    if (isSvgCode(code)) {
      void svgRenderer.renderPartial(container, code);
    } else {
      void htmlRenderer.renderPartial(container, code);
      if (code.includes("<script") && hasHtmlVisuals(code)) {
        container.classList.add("scripts-loading");
      }
    }
  };
  var nextRequestId = 1;
  async function connectToHost() {
    try {
      window.addEventListener("message", (event) => {
        try {
          const data = event.data;
          if (data && data.jsonrpc === "2.0") {
            if (data.id === 1 && data.result) {
              initHostContext(data.result);
              window.parent.postMessage(
                {
                  jsonrpc: "2.0",
                  method: "ui/notifications/initialized",
                  params: {}
                },
                "*"
              );
            }
          }
        } catch (err) {
          console.error("[VisualizeWidget] Error handling message:", err);
        }
      });
      app.onNotification("ui/notifications/tool-input-partial", (params) => {
        const args = params?.arguments ?? {};
        if (typeof args.title === "string" && args.title) vizTitle = args.title;
        const code = args.widget_code ?? args.code;
        if (code) renderPartialCode(code);
      });
      app.onNotification("ui/notifications/tool-input", (params) => {
        const args = params?.arguments ?? {};
        if (typeof args.title === "string" && args.title) vizTitle = args.title;
        const code = args.widget_code ?? args.code;
        if (code) {
          void renderFinal(code);
          if (!document.querySelector("form.elicit")) {
            document.getElementById("action-btns")?.removeAttribute("hidden");
          }
        }
      });
      window.parent.postMessage(
        {
          jsonrpc: "2.0",
          id: nextRequestId++,
          method: "ui/initialize",
          params: {
            protocolVersion: "2025-11-21",
            appInfo: IMPLEMENTATION,
            appCapabilities: {}
          }
        },
        "*"
      );
      app.setupAutoResize();
      const hostStr = (key) => {
        const s = window.__hostStrings;
        const v = s?.[key];
        return v && v.trim() ? v : void 0;
      };
      const FREEZE_PROPS = [
        "fill",
        "stroke",
        "color",
        "stroke-width",
        "stroke-dasharray",
        "stroke-linecap",
        "stroke-linejoin",
        "opacity",
        "font-family",
        "font-size",
        "font-weight",
        "font-style",
        "text-anchor",
        "dominant-baseline"
      ];
      const freezeSvgForDownload = () => {
        const live = container.querySelector("svg");
        if (!live) return null;
        const clone = live.cloneNode(true);
        if (!clone.hasAttribute("xmlns")) {
          clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
        }
        clone.style.removeProperty("max-width");
        clone.style.removeProperty("display");
        clone.style.removeProperty("margin-inline");
        const liveEls = live.querySelectorAll("*");
        const cloneEls = clone.querySelectorAll("*");
        for (let i = 0; i < liveEls.length; i++) {
          const el = liveEls[i];
          const out = cloneEls[i];
          if (el.closest("defs, marker, mask, clipPath, pattern, symbol")) {
            continue;
          }
          const cs = getComputedStyle(el);
          const decls = [];
          for (const p of FREEZE_PROPS) {
            const v = cs.getPropertyValue(p);
            if (!v) continue;
            if (v.startsWith("url(")) continue;
            if (v === "normal") continue;
            if (v === "none" && p === "stroke-dasharray") continue;
            decls.push(`${p}:${v}`);
          }
          if (decls.length) {
            const existing = out.getAttribute("style");
            out.setAttribute(
              "style",
              existing ? `${existing};${decls.join(";")}` : decls.join(";")
            );
          }
          out.removeAttribute("class");
        }
        clone.querySelectorAll("style, script").forEach((n) => n.remove());
        return new XMLSerializer().serializeToString(clone);
      };
      const downloadBtn = document.getElementById("download-btn");
      const downloadIconEl = document.getElementById("download-icon");
      const downloadLabelEl = downloadBtn?.querySelector(".more-item-label");
      const downloadIcon = '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path d="M16.5 13C16.7761 13 17 13.2239 17 13.5V15.5C17 16.3284 16.3284 17 15.5 17H4.5C3.67157 17 3 16.3284 3 15.5V13.5C3 13.2239 3.22386 13 3.5 13C3.77614 13 4 13.2239 4 13.5V15.5C4 15.7761 4.22386 16 4.5 16H15.5C15.7761 16 16 15.7761 16 15.5V13.5C16 13.2239 16.2239 13 16.5 13ZM10 3C10.2761 3 10.5 3.22386 10.5 3.5V12.1855L13.626 8.66797C13.8094 8.46166 14.1256 8.44275 14.332 8.62598C14.5383 8.80936 14.5573 9.12563 14.374 9.33203L10.374 13.832L10.2949 13.9033C10.21 13.9654 10.107 14 10 14C9.85718 14 9.72086 13.9388 9.62598 13.832L5.62598 9.33203L5.56738 9.25C5.45079 9.04872 5.48735 8.78653 5.66797 8.62598C5.84854 8.46567 6.1127 8.46039 6.29883 8.59961L6.37402 8.66797L9.5 12.1855V3.5C9.5 3.22386 9.72386 3 10 3Z"/></svg>';
      const downloadCheckIcon = '<svg width="20" height="20" viewBox="0 0 20 20" fill="var(--color-text-success, #265b19)"><path d="M15.1883 5.10908C15.3699 4.96398 15.6346 4.96153 15.8202 5.11592C16.0056 5.27067 16.0504 5.53125 15.9403 5.73605L15.8836 5.82003L8.38354 14.8202C8.29361 14.9279 8.16242 14.9925 8.02221 14.9989C7.88203 15.0051 7.74545 14.9526 7.64622 14.8534L4.14617 11.3533L4.08172 11.2752C3.95384 11.0811 3.97542 10.817 4.14617 10.6463C4.31693 10.4755 4.58105 10.4539 4.77509 10.5818L4.85321 10.6463L7.96556 13.7586L15.1161 5.1794L15.1883 5.10908Z"/></svg>';
      if (downloadIconEl) downloadIconEl.innerHTML = downloadIcon;
      window.addEventListener("viz:complete", (ev) => {
        const detail = ev.detail;
        const code = detail?.code;
        const title = detail?.title || "visualize";
        if (!code || !downloadBtn || !downloadIconEl || !downloadLabelEl) return;
        const isSvg = code.trimStart().startsWith("<svg");
        const ext = isSvg ? "svg" : "html";
        const mime = isSvg ? "image/svg+xml" : "text/html";
        downloadBtn.onclick = async () => {
          try {
            sendAction("download");
            const res = await app.sendRequest({
              method: "ui/download-file",
              params: {
                contents: [
                  {
                    type: "resource",
                    resource: {
                      uri: `file:///${title}.${ext}`,
                      mimeType: mime,
                      text: isSvg ? freezeSvgForDownload() ?? code : code
                    }
                  }
                ]
              }
            });
            if (!res?.isError) {
              const restore = downloadLabelEl.textContent;
              downloadIconEl.innerHTML = downloadCheckIcon;
              downloadLabelEl.textContent = hostStr("grabDone") ?? "Downloaded";
              setTimeout(() => {
                downloadIconEl.innerHTML = downloadIcon;
                downloadLabelEl.textContent = restore;
              }, 1500);
            }
          } catch {
          }
        };
      });
      const copyBtn = document.getElementById("copy-btn");
      const copyIconEl = document.getElementById("copy-icon");
      const copyLabelEl = copyBtn?.querySelector(".more-item-label");
      const copyIcon = '<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor"><path d="M12.5 3C13.3284 3 14 3.67157 14 4.5V6H15.5C16.3284 6 17 6.67157 17 7.5V15.5C17 16.3284 16.3284 17 15.5 17H7.5C6.67157 17 6 16.3284 6 15.5V14H4.5C3.67157 14 3 13.3284 3 12.5V4.5C3 3.67157 3.67157 3 4.5 3H12.5ZM14 12.5C14 13.3284 13.3284 14 12.5 14H7V15.5C7 15.7761 7.22386 16 7.5 16H15.5C15.7761 16 16 15.7761 16 15.5V7.5C16 7.22386 15.7761 7 15.5 7H14V12.5ZM4.5 4C4.22386 4 4 4.22386 4 4.5V12.5C4 12.7761 4.22386 13 4.5 13H12.5C12.7761 13 13 12.7761 13 12.5V4.5C13 4.22386 12.7761 4 12.5 4H4.5Z"/></svg>';
      const copyCheckIcon = '<svg width="20" height="20" viewBox="0 0 20 20" fill="var(--color-text-success, #265b19)"><path d="M15.1883 5.10908C15.3699 4.96398 15.6346 4.96153 15.8202 5.11592C16.0056 5.27067 16.0504 5.53125 15.9403 5.73605L15.8836 5.82003L8.38354 14.8202C8.29361 14.9279 8.16242 14.9925 8.02221 14.9989C7.88203 15.0051 7.74545 14.9526 7.64622 14.8534L4.14617 11.3533L4.08172 11.2752C3.95384 11.0811 3.97542 10.817 4.14617 10.6463C4.31693 10.4755 4.58105 10.4539 4.77509 10.5818L4.85321 10.6463L7.96556 13.7586L15.1161 5.1794L15.1883 5.10908Z"/></svg>';
      if (copyIconEl) copyIconEl.innerHTML = copyIcon;
      if (copyBtn && copyIconEl && copyLabelEl) {
        copyBtn.onclick = async () => {
          try {
            sendAction("copy");
            await copyToClipboard(container);
            const restore = copyLabelEl.textContent;
            copyIconEl.innerHTML = copyCheckIcon;
            copyLabelEl.textContent = hostStr("clipDone") ?? "Copied";
            setTimeout(() => {
              copyIconEl.innerHTML = copyIcon;
              copyLabelEl.textContent = restore;
            }, 1500);
          } catch (_err) {
            const toast = document.getElementById("copy-toast");
            toast.textContent = "Copy failed \u2014 try again in the host browser";
            toast.classList.add("visible");
            setTimeout(() => toast.classList.remove("visible"), 3e3);
          }
        };
      }
    } catch (err) {
      console.error("[VisualizeWidget] Failed to connect:", err);
    }
  }
  connectToHost();
})();
