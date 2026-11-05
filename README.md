# עמדת טעינה רב-קו — Windows

אפליקציית Windows בממשק עברי, עם שני שירותי טעינה רשמיים בתוך חלון WebView2 יחיד:
- HopOn — מתחיל ב-`https://hopon.co.il/read`.
- רב-קו אונליין — מתחיל ב-`https://ravkavonline.co.il/he/store/connect`.

## התקנה עצמאית בקובץ אחד

GitHub Actions מייצר קובץ `RavKavKiosk-Setup.exe` יחיד הכולל:
- יישום Windows שפורסם כ-self-contained, כך שאין צורך להתקין .NET בנפרד.
- המתקין בודק אם WebView2 Runtime כבר קיים; רק אם חסר, הוא מתקין את גרסת Evergreen Standalone x64 הכלולה בחבילה.
- קיצור דרך בתפריט התחל ואפשרות לקיצור דרך בשולחן העבודה.

המתקין דורש Windows x64 והרשאות מנהל. עדכוני WebView2 בעתיד מנוהלים על ידי מנגנון העדכון של Microsoft.

## הורדת המתקין

לאחר ש-GitHub Actions יסיים בנייה בהצלחה, קובץ ההתקנה יפורסם ב:
https://github.com/g0534160689-create/MyFirstApp/releases/latest

תהליך הבנייה:
https://github.com/g0534160689-create/MyFirstApp/actions

## מדיניות ניווט

### HopOn
נקודת הכניסה היא `/read`. שאר הניווט הפנימי באתר נשאר זמין, למעט הנתיבים הבאים שנחסמים, כולל תתי-נתיבים, וריאציות של אותיות גדולות/קטנות וקידודי URL:
- `/charging-stations-map`
- `/about`
- `/ravkav`
- `/freeMonthly`
- `/freeMonthlyOtef`
- `/accessibility`
- `/map`
- `/student`

### רב-קו אונליין
נקודת הכניסה היחידה לניווט ראשי יזום היא:
`https://ravkavonline.co.il/he/store/connect`

קישורי עוגן לעמודים אחרים באתר נחסמים. ניווט ראשי ישיר לעמוד אחר נחסם גם הוא. כדי לא לשבור את תהליך הטעינה, היישום מאפשר מעברי SPA פנימיים, שליחת טפסים וניתובים אוטומטיים שאינם יזומים ישירות על ידי המשתמש, וכן מסגרות פנימיות ומשאבי JavaScript/CSS/API. כך נתיב הכניסה נשאר מוגבל, אך זרימת הטעינה יכולה להמשיך בתוך היישום. פתיחת חלונות חיצוניים, הורדות ופרוטוקולים חיצוניים עדיין חסומים.

החסימות מותקנות לפני טעינת האתר ונבדקות בשכבות ניווט ראשי, מסגרות, בקשות מסמכי HopOn וסקריפט צד-לקוח. כללי רב-קו אונליין אינם חוסמים בקשות API ומשאבים טכניים של השירות.

## דרישות ומגבלות

- נדרש Windows 10/11 x64 וחיבור אינטרנט.
- WebView2 מוטמע בתוך חלון WinForms; אין פתיחת דפדפן Edge נפרד.
- סביבת WebView2 אחת ופרופיל משתמש ייעודי תחת `%LOCALAPPDATA%\\RavKavKiosk\\WebView2`.
- קורא כרטיסים, שירותי תשלום, חלונות תשלום חיצוניים ותהליך הטעינה בפועל דורשים בדיקה פיזית מול קורא USB וכרטיס אמיתי. בדיקת CI מאמתת התקנה, אתחול WebView2 וכללי ניווט, אך אינה מבצעת עסקת טעינה.
- אין איסוף או שמירה של פרטי אשראי בתוכנה.
- במקרה של שגיאה, הפרטים נשמרים תחת `%LOCALAPPDATA%\\RavKavKiosk\\Logs\\RavKavKiosk.log`.

## פיתוח מקומי

נדרשים Windows 10/11, .NET 10 SDK, Inno Setup ורכיב WebView2 Runtime. לפרסום מקומי:
```powershell
dotnet publish RavKavKiosk/RavKavKiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
iscc installer/RavKavKiosk.iss
```
