# עמדת טעינה HopOn — Windows

אפליקציית Windows במצב עמדת שירות עצמי, עם ממשק עברי וטעינת שירות HopOn בחלון פנימי.

## התקנה עצמאית בקובץ אחד

GitHub Actions מייצר קובץ `RavKavKiosk-Setup.exe` יחיד הכולל:
- יישום Windows שפורסם כ-self-contained, כך שאין צורך להתקין .NET בנפרד.
- מתקין WebView2 Evergreen Standalone x64, כדי להתקין את רכיב הדפדפן כחלק מתהליך ההתקנה, ללא הורדה נוספת בזמן ההתקנה.
- קיצור דרך בתפריט התחל ואפשרות לקיצור דרך בשולחן העבודה.

המתקין דורש Windows x64 והרשאות מנהל. רכיב WebView2 הכלול במתקין עשוי להיות גדול, ולכן קובץ ההתקנה יהיה גדול יותר מהיישום עצמו. התקנת עדכוני WebView2 בעתיד מנוהלת על ידי מנגנון העדכון של Microsoft ודורשת אינטרנט.

## הורדת המתקין

לאחר ש-GitHub Actions יסיים בנייה בהצלחה, קובץ ההתקנה יפורסם ב:
https://github.com/g0534160689-create/MyFirstApp/releases/latest

תהליך הבנייה:
https://github.com/g0534160689-create/MyFirstApp/actions

## הגבלת ניווט

התוכנה מאפשרת ניווט פנימי באתר הרשמי `https://hopon.co.il`, וחוסמת רק את הנתיבים הבאים (כולל נתיבים פנימיים תחתיהם):
- `/charging-stations-map`
- `/about`
- `/ravkav`
- `/freeMonthly`
- `/freeMonthlyOtef`

תתי-דומיינים ואתרים אחרים אינם מורשים. חלונות חדשים לאתרים אחרים, הורדות וקישורים חיצוניים חסומים. משאבי האתר (JavaScript, CSS, תמונות וקריאות API) נטענים כרגיל.

## דרישות ומגבלות

- נדרש Windows 10/11 x64 וחיבור אינטרנט לשימוש בשירות HopOn.
- התקנת WebView2 כלולה בקובץ ההתקנה, אך עדכונים עתידיים של WebView2 מתקבלים דרך מנגנון Microsoft.
- תאימות קורא הכרטיסים ותהליך התשלום ל-WebView2 עדיין דורשת בדיקה פיזית. פרוטוקולים חיצוניים חסומים כרגע.
- אין איסוף או שמירה של פרטי אשראי בתוכנה.

## פיתוח מקומי

נדרשים Windows 10/11, .NET 10 SDK, Inno Setup ורכיב WebView2 Runtime. לפרסום מקומי:
```powershell
dotnet publish RavKavKiosk/RavKavKiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
iscc installer/RavKavKiosk.iss
```
