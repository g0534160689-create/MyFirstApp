# עמדת טעינה HopOn — Windows

אפליקציית Windows קלה במצב עמדת שירות עצמי. מסך הפתיחה בעברית, וממנו נפתח שירות HopOn בתוך WebView2.

## הגבלת ניווט

התוכנה מאפשרת ניווט פנימי באתר הרשמי `https://hopon.co.il`, כדי שהשירות יוכל לעבור בין הדפים הדרושים לפעולתו.

רק הנתיבים הבאים חסומים (כולל נתיבים פנימיים תחתיהם):
- `/charging-stations-map`
- `/about`
- `/ravkav`
- `/freeMonthly`
- `/freeMonthlyOtef`

תתי-דומיינים ואתרים אחרים אינם מורשים. גם חלונות חדשים לאתרים אחרים, הורדות וקישורים חיצוניים חסומים. משאבי האתר (JavaScript, CSS, תמונות וקריאות API) נטענים כרגיל כדי לא לשבור את השירות.

## הורדת EXE

לאחר ש-GitHub Actions יסיים בנייה בהצלחה, קובץ EXE יפורסם ב:
https://github.com/g0534160689-create/MyFirstApp/releases/latest

תהליך הבנייה:
https://github.com/g0534160689-create/MyFirstApp/actions

היישום נבנה כ-Windows x64 self-contained. נדרש WebView2 Runtime במחשב היעד.

## מגבלות חשובות

- יש לבדוק בפועל שהאתר אינו משנה את מסלולי הניווט שלו.
- לא אומתה עדיין תאימות קורא הכרטיסים והתשלום ל-WebView2. כל פרוטוקול חיצוני חסום, ולכן ייתכן שקורא USB או שירות תשלום שדורש הפעלת תוכנה חיצונית לא יפעל.
- יש לבדוק בפועל עם קורא USB וכרטיס לפני שימוש בעמדה אמיתית.
- אין איסוף או שמירה של פרטי אשראי בתוכנה.

## פיתוח מקומי

נדרשים Windows 10/11, .NET 10 SDK ו-WebView2 Runtime.

```powershell
dotnet restore .\RavKavKiosk\RavKavKiosk.csproj
dotnet build .\RavKavKiosk\RavKavKiosk.csproj -c Release
dotnet run --project .\RavKavKiosk\RavKavKiosk.csproj
```
