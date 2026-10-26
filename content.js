(() => {
  if (window.__ArabicKeyboardFixerV5) return;
  window.__ArabicKeyboardFixerV5 = true;

  const AR = {
    "q": "ض", "w": "ص", "e": "ث", "r": "ق", "t": "ف", "y": "غ", "u": "ع", "i": "ه", "o": "خ", "p": "ح", "[": "ج", "]": "د",
    "a": "ش", "s": "س", "d": "ي", "f": "ب", "g": "ل", "h": "ا", "j": "ت", "k": "ن", "l": "م", ";": "ك", "'": "ط",
    "z": "ئ", "x": "ء", "c": "ؤ", "v": "ر", "b": "لا", "n": "ى", "m": "ة", ",": "و", ".": "ز", "/": "ظ", "`": "ذ",
    "Q": "َ", "W": "ً", "E": "ُ", "R": "ٌ", "T": "لإ", "Y": "إ", "U": "‘", "I": "÷", "O": "×", "P": "؛",
    "A": "ِ", "S": "ٍ", "D": "]", "F": "[", "G": "لأ", "H": "أ", "J": "ـ", "K": "،", "L": "/",
    "Z": "~", "X": "ْ", "C": "}", "V": "{", "B": "لآ", "N": "آ", "M": "’", "<": ",", ">": ".", "?": "؟"
  };

  const EN = {
    "ض": "q", "ص": "w", "ث": "e", "ق": "r", "ف": "t", "غ": "y", "ع": "u", "ه": "i", "خ": "o", "ح": "p", "ج": "[", "د": "]",
    "ش": "a", "س": "s", "ي": "d", "ب": "f", "ل": "g", "ا": "h", "ت": "j", "ن": "k", "م": "l", "ك": ";", "ط": "'",
    "ئ": "z", "ء": "x", "ؤ": "c", "ر": "v", "ى": "n", "ة": "m", "و": ",", "ز": ".", "ظ": "/", "ذ": "`",
    "أ": "H", "إ": "Y", "آ": "N", "؟": "?", "،": "K", "؛": "P"
  };

  const NON_TEXT_INPUT_TYPES = new Set([
    "button", "checkbox", "color", "date", "datetime-local", "file",
    "hidden", "image", "month", "radio", "range", "reset", "submit", "time", "week"
  ]);

  function convert(text) {
    if (!text) return text;
    const arabic = (text.match(/[\u0600-\u06FF]/g) || []).length;
    const english = (text.match(/[A-Za-z]/g) || []).length;

    if (arabic > english) {
      const normalized = text
        .replace(/لآ/g, "B")
        .replace(/لأ/g, "G")
        .replace(/لإ/g, "T")
        .replace(/لا/g, "b");
      return [...normalized].map(ch => EN[ch] ?? ch).join("");
    }
    return [...text].map(ch => AR[ch] ?? AR[ch.toLowerCase()] ?? ch).join("");
  }

  function getDeepActiveElement(root = document) {
    let el = root.activeElement;
    while (el && el.shadowRoot && el.shadowRoot.activeElement) {
      el = el.shadowRoot.activeElement;
    }
    return el;
  }

  function isTextInput(el) {
    if (!el || !el.tagName) return false;
    const tag = el.tagName.toUpperCase();
    if (tag === "TEXTAREA") {
      return !el.disabled && !el.readOnly;
    }
    if (tag === "INPUT") {
      const type = (el.type || "text").toLowerCase();
      return !NON_TEXT_INPUT_TYPES.has(type) && !el.disabled && !el.readOnly;
    }
    return false;
  }

  function isContentEditableElement(el) {
    if (!el) return false;
    if (el.isContentEditable) return true;
    if (document.designMode === "on") return true;
    const ceHost = el.closest?.('[contenteditable="true"], [contenteditable=""], [contenteditable="plaintext-only"]');
    return !!ceHost;
  }

  function isEditable(el) {
    if (!el || el === document.body || el === document.documentElement) {
      return document.designMode === "on" || !!el?.isContentEditable;
    }
    return isTextInput(el) || isContentEditableElement(el);
  }

  function getEditableHost(el) {
    if (!el) return null;
    if (isTextInput(el)) return el;
    const ceHost = el.closest?.('[contenteditable="true"], [contenteditable=""], [contenteditable="plaintext-only"]');
    if (ceHost) return ceHost;
    if (el.isContentEditable) return el;
    return null;
  }

  function dispatchInputEvent(el, text) {
    try {
      el.dispatchEvent(new InputEvent("input", { bubbles: true, cancelable: true, inputType: "insertText", data: text }));
    } catch {
      el.dispatchEvent(new Event("input", { bubbles: true }));
    }
    el.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function convertInput(el) {
    if (!isTextInput(el)) return false;
    el.focus();

    let s = null;
    let e = null;
    try {
      s = el.selectionStart;
      e = el.selectionEnd;
    } catch {
      s = null;
      e = null;
    }

    if (typeof s === "number" && typeof e === "number") {
      if (s !== e) {
        const selected = el.value.slice(s, e);
        const out = convert(selected);
        if (out === selected) return true;
        el.setRangeText(out, s, e, "select");
        dispatchInputEvent(el, out);
        return true;
      } else if (el.value) {
        const out = convert(el.value);
        if (out === el.value) return true;
        el.setRangeText(out, 0, el.value.length, "end");
        dispatchInputEvent(el, out);
        return true;
      }
      return false;
    }

    // Fallback for input types that do not support selectionStart (e.g. email)
    const sel = window.getSelection();
    const selText = sel ? sel.toString() : "";
    if (selText) {
      const out = convert(selText);
      if (document.execCommand("insertText", false, out)) {
        return true;
      }
    }
    if (el.value) {
      el.value = convert(el.value);
      dispatchInputEvent(el, el.value);
      return true;
    }
    return false;
  }

  function convertCE(activeEl) {
    const host = getEditableHost(activeEl);
    if (!host) return false;

    const sel = window.getSelection();
    if (!sel) return false;

    // Ensure selection is actually inside an editable host
    if (sel.rangeCount > 0) {
      let anchor = sel.anchorNode;
      if (anchor?.nodeType === 3) anchor = anchor.parentElement;
      if (anchor && !isEditable(anchor) && !host.contains(anchor)) {
        return false;
      }
    }

    let selectedText = sel.toString();
    let hadExplicitSelection = sel.rangeCount > 0 && !sel.isCollapsed && selectedText.length > 0;

    // If user didn't highlight text inside the editable box, select the contents of the current editable block/host
    if (!hadExplicitSelection) {
      const fullText = host.innerText || host.textContent || "";
      if (!fullText.trim()) return false;
      const range = document.createRange();
      range.selectNodeContents(host);
      sel.removeAllRanges();
      sel.addRange(range);
      selectedText = sel.toString();
      if (!selectedText) return false;
    }

    const out = convert(selectedText);
    if (out === selectedText) return true;

    host.focus();

    // Try execCommand first for full compatibility with React, Lexical, WhatsApp, Facebook, Notion, etc.
    let replaced = false;
    try {
      replaced = document.execCommand("insertText", false, out);
    } catch {
      replaced = false;
    }

    if (!replaced && sel.rangeCount > 0) {
      const range = sel.getRangeAt(0);
      range.deleteContents();
      const node = document.createTextNode(out);
      range.insertNode(node);
      sel.removeAllRanges();
      const newRange = document.createRange();
      if (hadExplicitSelection) {
        newRange.selectNodeContents(node);
      } else {
        newRange.setStartAfter(node);
        newRange.collapse(true);
      }
      sel.addRange(newRange);
      dispatchInputEvent(host, out);
    }

    return true;
  }

  let lastRunTime = 0;
  function run() {
    const now = Date.now();
    if (now - lastRunTime < 250) return false;

    const active = getDeepActiveElement();
    if (!isEditable(active)) return false;

    let ok = false;
    if (isTextInput(active)) {
      ok = convertInput(active);
    } else if (isContentEditableElement(active)) {
      ok = convertCE(active);
    }

    if (ok) {
      lastRunTime = now;
    }
    return ok;
  }

  let lastEditableState = null;
  function updateActionState() {
    if (!document.hasFocus()) return;
    const active = getDeepActiveElement();
    const editableNow = isEditable(active);
    if (editableNow !== lastEditableState) {
      lastEditableState = editableNow;
      chrome.runtime.sendMessage({ type: "EDITABLE_STATE", editable: editableNow }).catch(() => {});
    }
  }

  document.addEventListener("focusin", () => setTimeout(updateActionState, 10), true);
  document.addEventListener("focusout", () => setTimeout(updateActionState, 30), true);
  document.addEventListener("mousedown", () => setTimeout(updateActionState, 20), true);
  document.addEventListener("keyup", () => setTimeout(updateActionState, 10), true);
  window.addEventListener("focus", () => {
    lastEditableState = null;
    setTimeout(updateActionState, 10);
  });

  // Direct keyboard shortcut handler (Alt+Q) that works only inside editable fields
  window.addEventListener("keydown", (e) => {
    if (e.altKey && !e.ctrlKey && !e.metaKey && (e.code === "KeyQ" || e.key === "q" || e.key === "Q" || e.key === "ض")) {
      const active = getDeepActiveElement();
      if (isEditable(active)) {
        e.preventDefault();
        e.stopPropagation();
        run();
      }
    }
  }, true);

  chrome.runtime.onMessage.addListener((msg) => {
    if (msg?.type === "CONVERT_SELECTION") {
      run();
    }
  });

  if (document.hasFocus()) {
    updateActionState();
  }
})();
