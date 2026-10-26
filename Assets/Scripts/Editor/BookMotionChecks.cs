#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BookMotionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Senzora/Book Motion/Preview Live")]
    public static void PreviewLive()
    {
        Require(Application.isPlaying && ResearchPuzzleTrial.Active != null, "Start the laboratory scene in Play mode.");
        Require(GameManager.Instance.activeInteraction == null, "Exit the current interaction first.");
        ResearchPuzzleTrial.Active.StartCoroutine(Live());
    }

    private static IEnumerator Live()
    {
        Application.runInBackground = true;
        var game = GameManager.Instance;
        if (!game.isGameStarted)
        {
            UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().PlayGame();
            float deadline = Time.realtimeSinceStartup + 90;
            while (!game.isGameStarted && Time.realtimeSinceStartup < deadline) yield return null;
            Require(game.isGameStarted, "New game did not start.");
            yield return new WaitForSeconds(2);
        }
        var book = ResearchPuzzleTrial.Active.books[1];
        Vector3 home = book.transform.position;
        Quaternion rotation = book.transform.rotation;
        float duration = book.pageFlipDuration;
        float originalTimeScale = Time.timeScale;
        try
        {
        book.Interact();
        yield return new WaitForSeconds(.35f);
        ScreenCapture.CaptureScreenshot("Temp/book-motion-opening.png");
        yield return null;
        float end = Time.realtimeSinceStartup + 3;
        while (book.isAnimating && Time.realtimeSinceStartup < end) yield return null;
        Require(!book.isAnimating && book.isOpen, "Book opening did not finish within three seconds.");
        yield return null;
        ResearchTrialBookChecks.CheckReadability();
        ResearchTrialBookChecks.CollectVisible();
        ScreenCapture.CaptureScreenshot("Temp/book-motion-open.png");
        yield return null;
        if (book.totalPages >= 4)
        {
            int before = book.LeftPageIndex;
            book.pageFlipDuration = 1.2f;
            book.NextPage();
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot("Temp/book-motion-quarter.png");
            var canvas = book.GetComponentInChildren<Canvas>();
            Require(canvas != null && canvas.gameObject.activeInHierarchy, "Printed page disappeared during the turn.");
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot("Temp/book-motion-half.png");
            game.isGamePaused = true; Time.timeScale = 0;
            var paper = book.GetComponent<BookPageBlockSurface>();
            Vector3 frozen = book.transform.InverseTransformPoint((Vector3)Call(paper, "MapTurningPage", new Vector2(.8f, .4f)));
            yield return new WaitForSecondsRealtime(.15f);
            Require(Vector3.Distance(frozen, book.transform.InverseTransformPoint((Vector3)Call(paper, "MapTurningPage", new Vector2(.8f, .4f)))) < .00001f,
                "Paper moved while paused.");
            game.isGamePaused = false; Time.timeScale = 1;
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot("Temp/book-motion-three-quarter.png");
            while (book.isAnimating) yield return null;
            yield return null;
            Require(book.LeftPageIndex == before + 2, "Forward turn did not advance one spread.");
            ResearchTrialBookChecks.CheckReadability();
            book.pageFlipDuration = duration;
            book.PreviousPage();
            while (book.isAnimating) yield return null;
            Require(book.LeftPageIndex == before, "Backward turn did not restore the spread.");
            book.NextPage();
            book.ForceExit();
            Require(game.activeInteraction == book, "Exit during a turn should finish the paper before closing.");
            while (book.isAnimating) yield return null;
            yield return null;
        }
        book.pageFlipDuration = duration;
        if (book.isOpen) book.ForceExit();
        yield return new WaitForSeconds(.2f);
        ScreenCapture.CaptureScreenshot("Temp/book-motion-closing.png");
        while (book.isAnimating) yield return null;
        Require(Vector3.Distance(home, book.transform.position) < .0001f && Quaternion.Angle(rotation, book.transform.rotation) < .01f,
            "Closing did not restore the original book pose.");
        Require(game.activeInteraction == null, "Closing retained the interaction.");
        Debug.Log("PASS: Live natural book opening/closing, readable collectible ink, forward/backward turn, pause, queued exit and restored home pose. Frames: Temp/book-motion-*.png");
        }
        finally { Time.timeScale = originalTimeScale; game.isGamePaused = false; book.pageFlipDuration = duration; }
    }

    [MenuItem("Senzora/Doğrulama/Doğal Kitap Hareketi")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/P_Book.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var book = instance.GetComponent<InteractableBook>();
            book.printNotesOnPages = book.usePhysicalPageBlocks = true;
            book.totalPages = 8;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/NewBookAnim/newBookOpened.anim");
            clip.SampleAnimation(instance, 0);
            var surface = instance.GetComponent<BookPageBlockSurface>() ?? instance.AddComponent<BookPageBlockSurface>();
            typeof(BookPageBlockSurface).GetField("book", Private).SetValue(surface, book);
            typeof(InteractableBook).GetField("pageIndexL", Private).SetValue(book, 2);
            typeof(InteractableBook).GetField("pageIndexR", Private).SetValue(book, 3);
            Call(surface, "BuildSurface");
            Vector3 right = (Vector3)Call(surface, "MapPageWorld", new Vector2(.8f, .4f), true);
            Vector3 left = (Vector3)Call(surface, "MapPageWorld", new Vector2(.2f, .4f), false);
            foreach (int direction in new[] { 1, -1 })
            {
                Require((bool)Call(surface, "BeginTurn", direction), "Physical book must support a continuous paper turn.");
                Call(surface, "UpdateTurn", 0f);
                var paperMesh = book.pageFlipRenderer.GetComponent<MeshFilter>().sharedMesh;
                Vector3 paperNormal = book.pageFlipRenderer.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(paperMesh.normals[paperMesh.vertexCount / 2]).normalized;
                Require(Vector3.Dot(paperNormal, book.bookSkinnedMeshRenderer.transform.up) * direction > .5f,
                    "Turning paper winding swaps the front/back page textures.");
                Vector3 start = (Vector3)Call(surface, "MapTurningPage", new Vector2(.8f, .4f));
                Require(Vector3.Distance(start, direction > 0 ? right : left) < .0001f, "Turn must start on the original paper.");
                Call(surface, "UpdateTurn", .5f);
                Vector3 raised = (Vector3)Call(surface, "MapTurningPage", new Vector2(.8f, .4f));
                Require(Vector3.Distance(raised, Vector3.Lerp(left, right, .5f)) > Vector3.Distance(left, right) * .2f,
                    "Mid-turn paper must lift out of the book plane.");
                var canvas = (Canvas)typeof(BookPageBlockSurface).GetField("surface", Private).GetValue(surface);
                Require(!canvas.GetComponent<CanvasGroup>().blocksRaycasts, "Moving ink must not accept drag input.");
                Call(surface, "UpdateTurn", 1f);
                Vector3 end = (Vector3)Call(surface, "MapTurningPage", new Vector2(.8f, .4f));
                Require(Vector3.Distance(end, direction > 0 ? left : right) < .0001f, "Turn must land on the destination paper.");
                Call(surface, "EndTurn");
                Require(canvas.GetComponent<CanvasGroup>().blocksRaycasts, "Finished paper must accept drag input again.");
            }
            Debug.Log("PASS: Natural book forward/backward curl, attached surface endpoints, paper lift and input restoration.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static object Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null) throw new Exception("Missing book motion behavior: " + name);
        return method.Invoke(target, args);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
#endif
