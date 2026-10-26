const editableFrameByTab = new Map();

function setupContextMenu() {
  chrome.contextMenus.removeAll().then(() => {
    chrome.contextMenus.create({
      id: "convert-layout",
      title: "تحويل الكيبورد (Alt+Q)",
      contexts: ["editable"]
    });
  }).catch(() => {});
}

chrome.runtime.onInstalled.addListener(() => {
  setupContextMenu();
  chrome.action.disable().catch(() => {});
});

chrome.runtime.onStartup.addListener(() => {
  setupContextMenu();
  chrome.action.disable().catch(() => {});
});

async function triggerConversion(tabId, frameId) {
  if (!tabId) return;
  const targetFrameId = typeof frameId === "number" ? frameId : editableFrameByTab.get(tabId);
  const options = typeof targetFrameId === "number" ? { frameId: targetFrameId } : undefined;
  try {
    await chrome.tabs.sendMessage(tabId, { type: "CONVERT_SELECTION" }, options);
  } catch (e) {
    try {
      await chrome.scripting.executeScript({
        target: { tabId, allFrames: true },
        files: ["content.js"]
      });
      await chrome.tabs.sendMessage(tabId, { type: "CONVERT_SELECTION" }, options);
    } catch (e2) {}
  }
}

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === "convert-layout" && info.editable && tab?.id) {
    triggerConversion(tab.id, info.frameId);
  }
});

chrome.action.onClicked.addListener((tab) => {
  if (!tab?.id) return;
  if (!editableFrameByTab.has(tab.id)) return;
  triggerConversion(tab.id, editableFrameByTab.get(tab.id));
});

chrome.commands.onCommand.addListener(async (command) => {
  if (command === "convert-layout") {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab?.id) {
      triggerConversion(tab.id, editableFrameByTab.get(tab.id));
    }
  }
});

chrome.runtime.onMessage.addListener((msg, sender) => {
  const tabId = sender?.tab?.id;
  if (!tabId) return;
  if (msg?.type === "EDITABLE_STATE") {
    if (msg.editable) {
      editableFrameByTab.set(tabId, sender.frameId ?? 0);
      chrome.action.enable(tabId).catch(() => {});
    } else {
      editableFrameByTab.delete(tabId);
      chrome.action.disable(tabId).catch(() => {});
    }
  }
});

chrome.tabs.onRemoved.addListener((tabId) => {
  editableFrameByTab.delete(tabId);
});
