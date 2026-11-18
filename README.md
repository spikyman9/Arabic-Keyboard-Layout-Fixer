# ⌨️ مُحوّل الكيبورد الذكي | Arabic Keyboard Layout Fixer (v1.1.0)

<p align="center">
  <img src="icons/icon128.png" alt="Arabic Keyboard Fixer Logo" width="128" height="128" />
</p>

<p align="center">
  <b>أداة ويندوز خفيفة لتحويل النصوص المكتوبة باللغة الخطأ بين العربية والإنجليزية باختصار (Alt + Q).</b><br>
  <i>A lightweight Windows utility to fix mistyped Arabic/English keyboard layout text using (Alt + Q).</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%207%20|%208%20|%2010%20|%2011-blue.svg" alt="Platform" />
  <img src="https://img.shields.io/badge/Language-C%23-239120.svg?logo=c-sharp" alt="C#" />
  <img src="https://img.shields.io/badge/Version-1.1.0-success.svg" alt="Version" />
  <img src="https://img.shields.io/badge/License-MIT-orange.svg" alt="License" />
</p>

---

## 📌 نبذة عن المشروع (About)

كم مرة كتبت رسالة أو بحثت عن شيء لتكتشف أنك كتبت `hsl; hpl]` بدلاً من `اسمك أحمد` أو `مرحبا` بدلاً من `lvpfh`؟

**مُحوّل الكيبورد (Arabic Keyboard Layout Fixer)** هو برنامج مكتبي خفيف لنظام ويندوز يعمل في الخلفية (System Tray). بمجرد ضغط الاختصار **`Alt + Q`**، يقوم البرنامج بتحويل النص الخاطئ في مكانه وتبديل لغة الكيبورد تلقائياً دون الحاجة لمسح النص أو إعادة كتابته.

---

## ✨ المميزات الرئيسية (Key Features)

- ⚡ **يعمل في معظم برامج الويندوز:** المتصفحات (Chrome, Edge, Firefox)، برامج المراسلة (WhatsApp Desktop, Telegram, Discord, Facebook Chat)، وحزم الأوفيس ومحررات النصوص (Word, Notepad, VS Code, Visual Studio).
- 🧠 **تحويل ذكي للنص المختلط:**
  - **بدون تحديد:** يحوّل آخر مقطع مكتوب باللغة الخطأ في السطر الحالي فقط، ويترك الكلام الصحيح قبله كما هو.
  - **مع التحديد:** يحوّل الكلمات المكتوبة باللغة الغالبة الخاطئة ويحافظ على الكلمات الصحيحة باللغة الأخرى.
  - **دعم Caps Lock:** يكتشف الكتابة بحروف كبيرة كلها (`LVPFH` ← `مرحبا`) ويعيدها لوضعها الطبيعي.
- 📋 **حفظ الحافظة (Clipboard Safe):** يأخذ نسخة احتياطية من محتوى الحافظة (نصوص، صور، ملفات) ويعيده بعد التحويل، مع استثناء النص المؤقت من سجل الحافظة.
- 🌐 **تبديل آمن للغة الكيبورد:** يبدّل فقط بين اللغات المثبتة لديك فعلاً في ويندوز ولا يضيف لغات جديدة.
- 🛡️ **حماية محررات الأكواد والترمينال:**
  - يكتشف نسخ السطر الفارغ في VS Code و Visual Studio و Notepad++ لمنع تكرار السطور.
  - يتجاهل نوافذ الأوامر (CMD / PowerShell / Windows Terminal) حتى لا يرسل `Ctrl+C` ويوقف العمليات الجارية.
  - ينبهك إذا كانت النافذة النشطة تعمل بصلاحيات المسؤول (Administrator).
- 🔤 **دعم التخطيط القياسي (Arabic 101):** يشمل الحروف المركبة (`لا`، `لأ`، `لإ`، `لآ`)، التشكيل، وعلامات الترقيم العربية (`،`، `؛`، `؟`).

---

## 📥 التثبيت والتشغيل (Installation)

1. **النسخة المحمولة (Portable):** شغّل `ArabicKeyboardFixer.exe` مباشرة بدون تثبيت.
2. **حزمة التثبيت (Installer):** عبر `installer.iss` (باستخدام Inno Setup 6) لإنشاء اختصارات قائمة ابدأ وسطح المكتب وخيار التشغيل مع بدء ويندوز وإلغاء تثبيت نظيف.

---

## 🛠️ البناء من المصدر (Build from Source)

المشروع مكتوب بلغة C# (متوافق مع C# 5) ويستخدم مترجم .NET Framework المدمج في ويندوز:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

---

## 📋 متطلبات التشغيل (System Requirements)

- **نظام التشغيل:** Windows 7 / 8 / 8.1 / 10 / 11 (x86 / x64)
- **بيئة التشغيل:** .NET Framework 4.5 أو أحدث (مدمج في Windows 8/10/11، ومتاح للتثبيت على Windows 7).

---

## 📄 الترخيص (License)

هذا المشروع مفتوح المصدر تحت ترخيص **[MIT License](LICENSE)**.
