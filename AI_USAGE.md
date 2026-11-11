# AI Usage Disclosure

## 1. Tools Used
- ChatGPT (OpenAI)
- Claude (Anthropic)
- Gemini (Google DeepMind)

## 2. Where AI Was Used
- Project scaffolding and structure setup for the .NET API and React frontend.
- Writing Dockerfile and docker-compose.yml configuration.
- Generating boilerplate code for xUnit automated tests.
- Clean code suggestions, refactoring ideas, and code structuring.

## 3. What AI Did Wrong and How I Fixed It

### 1. Test Data Mixed with Live App Data
- **Problem:** AI initially configured the automated test container to use the same database as the main application.
- **How I Found It:** After running tests, I opened the web browser and saw roughly 100 test classes polluting the class list in the UI.
- **Fix:** Pointed the test configuration to an isolated test database (`school_admission_test`) so running tests never touches real application data.

### 2. Expired Seat Blocking New Applications
- **Problem:** When a student's hold expired, the initial code still considered them as having an active application, preventing them from applying again.
- **How I Found It:** I tested with a 2-second hold (`HOLD_DURATION_SECONDS=2`), waited 3 seconds, and tried applying again. The request returned an error instead of letting me apply.
- **Fix:** Updated the logic to mark old expired holds as `EXPIRED` before checking application limits, allowing students to re-apply after their time runs out.

### 3. Payment Button Disappearing on Expiry
- **Problem:** In the React UI, the "Pay Admission Fee" button completely disappeared from the card once a hold expired.
- **How I Found It:** The requirement specifically says that when time expires, the button should become disabled (turned off), not removed from the screen.
- **Fix:** Updated the component to keep the button visible on the card but disabled when the hold status is `EXPIRED`.
