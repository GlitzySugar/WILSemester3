using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // if you use TextMeshPro

public class TeacherQuizManager : MonoBehaviour
{
    [Header("Quiz settings")]
    public int totalQuestions = 10;
    public int requiredToWin = 5;

    [Header("Difficulty / hunger integration")]
    // If you have your own hunger system, set this reference in inspector.
    // If left null, the SimpleHungerStub will be used for testing.
    public MonoBehaviour hungerProvider; // expects IPlayerHunger (see interface below)
    public bool useMashMechanic = false; // if true uses mash mechanic instead of timing for answers

    [Header("UI references")]
    public GameObject quizPanel; // parent panel to enable/disable
    public TMP_Text questionText;
    public TMP_Text[] answerTexts = new TMP_Text[4]; // 4 choices
    public Button debugNextButton; // optional for debugging

    [Header("Controllers")]
    public TimingBarController timingBar; // required for timing-selection mode
    public MashEController mashController; // optional for mash mode

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onQuizWin;
    public UnityEngine.Events.UnityEvent onQuizFail;
    public UnityEngine.Events.UnityEvent onQuestionShown;
    public UnityEngine.Events.UnityEvent onQuestionAnswered;

    // internal
    List<Question> questions;
    int currentIndex = -1;
    int correctCount = 0;
    int answeredCount = 0;
    bool isRunning = false;

    IPlayerHunger hunger => hungerProvider as IPlayerHunger;

    void Start()
    {
        // For safety: if hungerProvider not set, try to find a SimpleHungerStub in scene
        if (hunger == null)
        {
            var stub = FindObjectOfType<SimpleHungerStub>();
            if (stub != null) hungerProvider = stub;
        }

        if (quizPanel) quizPanel.SetActive(false);

        if (debugNextButton) debugNextButton.onClick.AddListener(() => NextQuestionForced());
    }

    // Public API to start the quiz
    public void StartQuiz()
    {
        preparedQuestions();
        currentIndex = -1;
        correctCount = 0;
        answeredCount = 0;
        isRunning = true;
        if (quizPanel) quizPanel.SetActive(true);
        NextQuestion();
    }

    void preparedQuestions()
    {
        questions = new List<Question>();
        System.Random rng = new System.Random();
        for (int i = 0; i < totalQuestions; i++)
        {
            questions.Add(Question.GenerateSimple(rng));
        }
    }

    void NextQuestion()
    {
        if (!isRunning) return;

        currentIndex++;
        if (currentIndex >= questions.Count)
        {
            EndQuiz();
            return;
        }

        var q = questions[currentIndex];

        // Hunger-based skipping logic:
        // If starving, high chance the player "misses" the question (i.e., it's skipped and counts as missed).
        // If hungry, lower chance. If normal, no auto-skip.
        var state = hunger != null ? hunger.GetHungerState() : HungerState.Normal;

        float skipChance = 0f;
        if (state == HungerState.Starving) skipChance = 0.6f; // 60% chance to miss (tweak)
        else if (state == HungerState.Hungry) skipChance = 0.25f; // 25% chance
        else skipChance = 0f;

        if (UnityEngine.Random.value < skipChance)
        {
            // skip (counts as missed)
            answeredCount++;
            Debug.Log($"Question {currentIndex + 1} skipped due to hunger state {state}.");
            // fire answered event (so UI/journal can log)
            onQuestionAnswered?.Invoke();
            NextQuestion(); // move to next immediately
            return;
        }

        // Show the question
        ShowQuestionUI(q);
    }

    void ShowQuestionUI(Question q)
    {
        if (questionText != null) questionText.text = $"Q{currentIndex + 1}: {q.questionString}";
        for (int i = 0; i < 4; i++)
        {
            answerTexts[i].text = q.choices[i].ToString();
        }

        onQuestionShown?.Invoke();

        if (useMashMechanic && mashController)
        {
            // Mash mechanic mode: player first chooses an answer by pressing 1-4, then must mash to confirm
            StartCoroutine(MashFlow(q));
        }
        else
        {
            // Timing bar mode: player uses timing bar to pick a slot (0..3)
            StartCoroutine(TimingFlow(q));
        }
    }

    IEnumerator TimingFlow(Question q)
    {
        // start the timing bar and wait for selection
        timingBar.gameObject.SetActive(true);
        timingBar.StartSweep();

        bool answered = false;
        int selectedIndex = -1;

        timingBar.onSelectionComplete = (idx) =>
        {
            selectedIndex = idx;
            answered = true;
        };

        // wait until answered
        while (!answered) yield return null;

        timingBar.StopSweep();
        timingBar.gameObject.SetActive(false);

        ProcessAnswer(q, selectedIndex);
        yield return new WaitForSeconds(0.35f); // small delay
        NextQuestion();
    }

    IEnumerator MashFlow(Question q)
    {
        // Simple mash flow: first wait for player to press 1-4 to choose answer
        int chosen = -1;
        bool chose = false;

        // display a small UI hint: "Press 1-4 to choose"
        // We'll watch input here
        while (!chose)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { chosen = 0; chose = true; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { chosen = 1; chose = true; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { chosen = 2; chose = true; }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { chosen = 3; chose = true; }
            yield return null;
        }

        // now run mash controller to confirm
        mashController.gameObject.SetActive(true);
        mashController.ResetMeter();
        bool success = false;
        mashController.onMashComplete = (s) => success = s;
        mashController.StartMash();

        while (!mashController.isComplete) yield return null;

        mashController.gameObject.SetActive(false);

        // if mash success, treat chosen as final; if not, mark wrong
        int finalIndex = success ? chosen : -1; // -1 == wrong by missing
        ProcessAnswer(q, finalIndex);

        yield return new WaitForSeconds(0.35f);
        NextQuestion();
    }

    void ProcessAnswer(Question q, int selectedIndex)
    {
        answeredCount++;
        bool correct = (selectedIndex == q.correctChoiceIndex);
        if (correct) correctCount++;

        Debug.Log($"Answered Q{currentIndex + 1}: selected {selectedIndex} correctIndex {q.correctChoiceIndex} => {correct}");

        onQuestionAnswered?.Invoke();
        CheckEarlyEnd();
    }

    void CheckEarlyEnd()
    {
        // optional: early win/lose if remaining questions can't change outcome
        int remaining = totalQuestions - answeredCount;
        // if even if player gets all remaining correct they can't reach requiredToWin => fail
        if (correctCount + remaining < requiredToWin)
        {
            EndQuiz();
        }
        else if (correctCount >= requiredToWin)
        {
            // early win is allowed if player already reached threshold
            EndQuiz();
        }
    }

    void EndQuiz()
    {
        isRunning = false;
        if (quizPanel) quizPanel.SetActive(false);

        if (correctCount >= requiredToWin)
        {
            Debug.Log($"Quiz WIN: {correctCount}/{totalQuestions}");
            onQuizWin?.Invoke();
        }
        else
        {
            Debug.Log($"Quiz FAIL: {correctCount}/{totalQuestions}");
            onQuizFail?.Invoke();
        }
    }

    // debug helper
    public void NextQuestionForced()
    {
        NextQuestion();
    }
}


// ------------------------------
// Helper types
// ------------------------------
public enum HungerState { Normal, Hungry, Starving }

public interface IPlayerHunger
{
    HungerState GetHungerState();
}

// question representation
[System.Serializable]
public class Question
{
    public string questionString;
    public int correctAnswer;
    public int[] choices = new int[4];
    public int correctChoiceIndex;

    public static Question GenerateSimple(System.Random rng)
    {
        // generate simple addition/subtraction/multiplication
        int a = rng.Next(1, 13);
        int b = rng.Next(1, 13);
        int op = rng.Next(0, 3); // 0:+ 1:- 2:*
        string qs;
        int ans;
        if (op == 0) { qs = $"{a} + {b}"; ans = a + b; }
        else if (op == 1) { qs = $"{a} - {b}"; ans = a - b; }
        else { qs = $"{a} × {b}"; ans = a * b; }

        var q = new Question();
        q.questionString = qs;
        q.correctAnswer = ans;

        // fill choices (randomize)
        List<int> pool = new List<int> { ans };
        while (pool.Count < 4)
        {
            int delta = rng.Next(-6, 7);
            int candidate = ans + delta;
            if (candidate == ans) continue;
            if (candidate < -20 || candidate > 200) continue;
            if (!pool.Contains(candidate)) pool.Add(candidate);
        }

        // shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int j = rng.Next(i, pool.Count);
            int temp = pool[i]; pool[i] = pool[j]; pool[j] = temp;
        }

        q.choices = pool.ToArray();
        q.correctChoiceIndex = Array.IndexOf(q.choices, ans);
        return q;
    }
}
