// Online workshops: the live chat on a workshop's page, and the form's local times. The chat's forms work without
// JavaScript (each send reloads the page); this adds new messages as they come and sends without a reload.
(() => {
  const log = document.querySelector("[data-chat-log]");
  if (log) {
    const list = log.querySelector("[data-chat-messages]");
    const empty = log.querySelector("[data-chat-empty]");
    const form = document.querySelector("[data-chat-form]");
    const token = () => document.querySelector("input[name=__RequestVerificationToken]")?.value ?? "";
    let lastId = Number(log.dataset.lastId) || 0;
    let busy = false;

    const when = (iso) => new Date(iso).toLocaleString(undefined, { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
    const nearBottom = () => log.scrollHeight - log.scrollTop - log.clientHeight < 80;
    const scrollDown = () => { log.scrollTop = log.scrollHeight; };

    // Built with textContent only, so a message can never become markup.
    const render = (m) => {
      const li = document.createElement("li");
      li.className = "chat-message" + (m.isHost ? " is-host" : "");
      li.dataset.messageId = String(m.id);

      let avatar;
      if (m.author.avatarUrl) {
        avatar = document.createElement("img");
        avatar.className = "avatar"; avatar.src = m.author.avatarUrl; avatar.alt = ""; avatar.referrerPolicy = "no-referrer";
      } else {
        avatar = document.createElement("span");
        avatar.className = "avatar avatar-initial"; avatar.setAttribute("aria-hidden", "true");
        avatar.textContent = m.author.displayName.slice(0, 1);
      }

      const body = document.createElement("div");
      body.className = "comment-body";
      const head = document.createElement("div");
      head.className = "comment-head";
      const name = document.createElement("a");
      name.href = log.dataset.memberUrl.replace("00000000-0000-0000-0000-000000000000", m.author.id);
      name.textContent = m.author.displayName;
      head.append(name);
      if (m.isHost) {
        const badge = document.createElement("span");
        badge.className = "chat-host-badge"; badge.textContent = "host";
        head.append(badge);
      }
      const time = document.createElement("time");
      time.dateTime = m.createdAt; time.textContent = when(m.createdAt);
      head.append(time);
      if (m.canDelete) {
        const del = document.createElement("form");
        del.method = "post"; del.className = "comment-delete"; del.dataset.chatDelete = "";
        del.action = `${log.dataset.deleteUrl}?messageId=${m.id}`;
        const button = document.createElement("button");
        button.type = "submit"; button.className = "btn-link-muted"; button.textContent = "Delete";
        button.setAttribute("aria-label", "Delete message");
        del.append(button);
        head.append(del);
      }
      const text = document.createElement("p");
      text.textContent = m.body;
      body.append(head, text);
      li.append(avatar, body);
      return li;
    };

    const add = (messages) => {
      const stick = nearBottom();
      for (const m of messages) {
        if (m.id <= lastId || list.querySelector(`[data-message-id="${m.id}"]`)) continue;
        list.append(render(m));
        lastId = Math.max(lastId, m.id);
      }
      if (empty) empty.hidden = list.children.length > 0;
      if (stick) scrollDown();
    };

    const poll = async () => {
      if (busy || document.hidden) return;
      busy = true;
      try {
        const response = await fetch(`${log.dataset.messagesUrl}?after=${lastId}`, { headers: { "X-Requested-With": "fetch" } });
        if (response.ok) add(await response.json());
      } catch { /* offline for a moment: try again next time */ }
      finally { busy = false; }
    };

    scrollDown();
    setInterval(poll, Number(log.dataset.refreshMs) || 5000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) poll(); });

    form?.addEventListener("submit", async (event) => {
      event.preventDefault();
      const box = form.querySelector("textarea");
      const error = form.querySelector("[data-chat-error]");
      const button = form.querySelector("button[type=submit]");
      if (!box.value.trim()) return;
      button.disabled = true;
      error.textContent = "";
      try {
        const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "fetch" } });
        if (response.redirected || response.status === 401) { form.submit(); return; }
        if (!response.ok) {
          error.textContent = (await response.json().catch(() => null))?.error ?? "That message couldn't be sent. Please try again.";
          return;
        }
        box.value = "";
        add([await response.json()]);
        scrollDown();
      } catch {
        error.textContent = "That message couldn't be sent. Check your connection and try again.";
      } finally {
        button.disabled = false;
        box.focus();
      }
    });

    // Enter sends, Shift+Enter starts a new line.
    form?.querySelector("textarea").addEventListener("keydown", (event) => {
      if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
        event.preventDefault();
        form.requestSubmit();
      }
    });

    list.addEventListener("submit", async (event) => {
      const del = event.target.closest("[data-chat-delete]");
      if (!del) return;
      event.preventDefault();
      if (!confirm("Delete this message?")) return;
      const data = new FormData();
      data.append("__RequestVerificationToken", token());
      const response = await fetch(del.action, { method: "POST", body: data, headers: { "X-Requested-With": "fetch" } });
      if (response.ok || response.status === 404) del.closest(".chat-message").remove();
      if (empty) empty.hidden = list.children.length > 0;
    });
  }

  // The form: show the stored start in the member's own time, and send their time zone with the local time.
  const form = document.querySelector("[data-workshop-form]");
  if (form) {
    const starts = form.querySelector("#starts-local");
    if (starts.dataset.startsUtc && !starts.value) {
      const d = new Date(starts.dataset.startsUtc);
      const pad = (n) => String(n).padStart(2, "0");
      starts.value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
    form.addEventListener("submit", () => {
      const local = starts.value ? new Date(starts.value) : new Date();
      form.querySelector("[data-utc-offset]").value = String(-local.getTimezoneOffset());
    });
  }
})();
