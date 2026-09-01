using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static SkillTree_Manager.SkillName;

public class SkillTree_Intro : MonoBehaviour
{
    private static WaitForSeconds _waitForSeconds2 = new WaitForSeconds(2f);
    private static WaitForSeconds _waitForSeconds1_5 = new WaitForSeconds(1.5f);
    private static WaitForSeconds _waitForSeconds0_2 = new WaitForSeconds(0.2f);
    private static WaitForSeconds _waitForSeconds0_1 = new WaitForSeconds(0.1f);
    [SerializeField] GameObject Overlay;
    [SerializeField] TMP_Text introText, insideGuideText;
    [SerializeField] GameObject CookiePlate, ExtraCookies, HerNote, ExtraStuff;

    private int CookiesCount => CookiePlate.transform.childCount;

    private SkillTree_Manager SkillTree_Manager;

    private void Awake()
    {
        HerNote.SetActive(PlayerPrefs.HasKey("InventionsUsed"));
        if (HerNote.activeSelf) UpdateHerNoteText();

        PlayerPrefs.SetInt("CookiesEaten", 0);
        ExtraStuff.SetActive(SaveDataManager.AllResearchesUnlocked);
    }

    private void OnEnable()
    {
        SkillTree_Manager = GetComponent<SkillTree_Manager>();

        if (PlayerPrefs.GetInt("ResearchIntroPlayed", 0) != 0)
        {
            Destroy(Overlay);
            SkillTree_Manager.enabled = true;
            return;
        }

        StartCoroutine(Intro());
    }

    void UpdateHerNoteText()
    {
        int currentHour = DateTime.Now.TimeOfDay.Hours;

        string greeting = "";

        bool isLate = false;

        if (currentHour > 5 && currentHour <= 11)
        {
            greeting = "Good morning!";
        }
        else if (currentHour > 11 && currentHour <= 17)
        {
            greeting = "Good afternoon!";
        }
        else if (currentHour > 17 && currentHour <= 23)
        {
            greeting = "Good evening!";
        }
        else
        {
            greeting = "Isn't it late now? You should take some rest.";
            isLate = true;
        }

        if (isLate)
        {
            insideGuideText.text = greeting;
            return;
        }

        string cont_1 = "\nIf you're reading it, it means I'm not at this place ATM." +
            "\nReally sorry, even though you took time to drop by... Maybe next time then...";

        int cookiesEaten = PlayerPrefs.GetInt("CookiesEaten", 0);
        if (cookiesEaten > 0)
        {
            bool ateAllCookies = cookiesEaten >= CookiesCount;
            string cookieTxt = ateAllCookies ?
                "\n\nAnyway, I'm glad you liked those cookies that much. I baked more this time!"
                :
                "\n\nAnyway, how's the cookies? Hope you liked them. I already baked another load, so enjoy!";

            cont_1 += cookieTxt;
            ExtraCookies.SetActive(ateAllCookies);
        }

        cont_1 += "\n\nAs usual, just pick whichever you need. I'm always hyped to see what amazing things you can do with them!";

        List<string> UsedSkillsTxt = PlayerPrefs.GetString("InventionsUsed", "").Split(" ").ToList();
        List<SkillTree_Manager.SkillName> UsedSkills = new();
        foreach (string item in UsedSkillsTxt)
        {
            SkillTree_Manager.SkillName Skill;
            try
            {
                Skill = (SkillTree_Manager.SkillName)Enum.Parse(typeof(SkillTree_Manager.SkillName), item.ToUpper());
            }
            catch
            {
                continue;
            }

            UsedSkills.Add(Skill);
        }
        UsedSkills = UsedSkills.OrderBy(s => s).ToList();

        string skillContent = string.Empty;
        if (UsedSkills.Count > 0)
        {
            cont_1 += "\n\nOh, and, about your last exploration:";

            foreach (var Skill in UsedSkills)
            {
                string itemDes = Skill switch
                {
                    WINGED_STEPS_A or WINGED_STEPS_B or WINGED_STEPS_C => "Those words I wrote on my art units? Ah, they are lyrics from songs I like a lot. Want to listen to them?",
                    EQUIPMENT_SCOPE => "Honeyberry really likes jumping off high places for someone to catch her, and that someone is usually me. " +
                    "It may sound dangerous, but don't worry, she always lands neatly in my arms. I'm pretty good at these sporty activities, after all!",
                    EQUIPMENT_RADIO => "Thanks to that radio, we were able to chat to eachother even when you were far away. It was so fun, sadly the battery ran out mid way...",
                    EQUIPMENT_BLADE => "Hehe... sorry, but I couldn't help imaging you wearing those cat paws during battle. It's cute, right?",
                    EQUIPMENT_PROVISIONS => "Exploring is fun and all, but don't forget to treat yourself properly. I already made some soup, it's kind of basic, but help yourself!",
                    GEOLOGIST_OBSERVE => "Seems like you've taken a liking in geology. Nature is beautiful. Isn't it?",
                    GEOLOGIST_STUDY => "Seems like you've taken a liking in geology. Nature is powerful. Isn't it?",
                    GEOLOGIST_EXPLORE => "Seems like you've taken a liking in geology. Nature is exciting. Isn't it?",
                    SMOKE_TOXIC => "Don't worry, before leaving, I already sprayed this bug spray everywhere. Now you can rest without worrying about bugs landing on your face anymore!",
                    SMOKE_BLIND => "That pepper spray is very dangerous. If it happens to get caught in your eyes, rinse it off with water immediately! " +
                    "Oh no... how will you even read this if that was really the case... I should have warned you earlier......",
                    BUBBLE_ARTS => "That bubble-making staff was really awesome, right? Aroma said that if the soap inside ever runs out, just bring it to her for refill.",
                    AMULET => "Did you get hurt somewhere? If anything, there's aid kit on the table, and some medicines on shelf too! Oh, and, make sure you take a good rest before leaving!!",
                    KNOTS => "These tied knots, let's keep it that way. Yes?",
                    A_NICE_LOOKING_ROCK => "I'm happy you liked that rock. Speaking of which, I found another one on my ways back the other day, and so many more pretty-looking stones just like it! But, well... that was before I realized I'd gotten separated from the others...",
                    ATTENTION_DEVICE => "Did the attention device work out? I never really try it, but I'll need it soon.",
                    ATTENTION_BOOK => "I'm surprised you could use my book. Did it bite you? I left some bandages on the table just in case...",
                    DASH_LETHAL => "That technique - \"Compress the air pressure, and release it at once...\", wasn't it? How did it feel?",
                    DASH_FAITH => "That technique - \"Compress the air pressure, and hold it around...\", wasn't it? Did it catch your opponents by surprise?",
                    DASH_AFTERIMAGES => "I wish I could participate in more research projects. My instructor and teammates always take such good care of me. They're worried my illness could get worse if I push myself too hard, so they don't let me join them on the surveys... I must get better so I can go back and focus on my research.",
                    BLACKFLASH => "That flashy technique you showed me was awesome. It is not easy to pull off at all, isn't it?",
                    WINDBLOW_NORTH or WINDBLOW_SOUTH => "My wind arts is pretty fun to play with, right?",
                    FREEZE_SUPERCONDUCT => "Speaking of the modified defroster. I still haven't come up a name for it...",
                    FREEZE_HOLD => "The technique that keeps the frozen things goes on forever... I wonder if we can make ice creams that would never melt with it...",
                    FREEZE_BLOOM => "The technique that makes the freeze spreads... It was so beautiful when you showed it here, but in the end it left everything frozen so the clean-up was pretty messy...",
                    SPIRAL_FIELD_EXPERT => "The samples you brought back were really amazing! To think we could fuse environment energy into our own projectiles like this, I really learnt a lot...",
                    WIND_ANTHEM => "It seems like you already got used to using my arts and equipments. I won't be surprised if you manage to surpass me someday, hehe.",
                    SPIRAL_READ => "As much as I'd like to improve my attention span, the current me can still work on stuff if I'm determined to!",
                    SPIRAL_SHADOW => "That clone technique is really cool!",
                    BEYOND_NIGHT => "If possible, let's gaze the stars again some day!",
                    _ => string.Empty,
                };

                if (itemDes == string.Empty || skillContent.Contains(itemDes)) continue;

                skillContent += $"\n\n{itemDes}";
            }

            if (skillContent == string.Empty)
                skillContent = " I hope my inventions helped!";
        }

        insideGuideText.text = greeting + cont_1 + skillContent 
            + "\n\nNow, there's still many more I want to say, but let's just wait until we get to meet, so that's about it.\nSee you later! Take care!";
    }

    public void OnCookieEat()
    {
        int cookieEaten = PlayerPrefs.GetInt("CookiesEaten", 0);
        PlayerPrefs.SetInt("CookiesEaten", cookieEaten + 1);
        PlayerPrefs.Save();
    }

    IEnumerator Intro()
    {
        yield return _waitForSeconds2;
        yield return StartCoroutine(FadeInText
            ("A private place filled with books, crafts, ideas, theories,\nand a bunch of everything else.", TEXT_APPEAR_TYPE.FADE_IN, 3f, 5f));
        yield return _waitForSeconds0_1;
        yield return StartCoroutine(FadeInText
            ("Each is a mystery awaiting to be figured out.", TEXT_APPEAR_TYPE.FADE_IN, 1f, 4f));
        yield return _waitForSeconds0_1;
        yield return StartCoroutine(FadeInText
            ("For example,\na first prototype of an <color=yellow>\"Attention-holding device\"</color>.",
            TEXT_APPEAR_TYPE.FADE_IN, 1f, 6f));
        yield return _waitForSeconds0_2;
        yield return StartCoroutine(FadeInText
            ("It's still untested,\njust like most of <color=#00FFD5>her</color> inventions here.",
            TEXT_APPEAR_TYPE.FADE_IN, 0.5f, 6f));
        yield return _waitForSeconds0_1;
        yield return StartCoroutine(FadeInText
            ("<color=#00FFD5>She</color> insists that it will work.", TEXT_APPEAR_TYPE.FADE_IN, 0.4f, 3f));
        yield return _waitForSeconds0_1;
        yield return StartCoroutine(FadeInText
            ("<color=#f800ff>I</color>, well, doubt that it will.", TEXT_APPEAR_TYPE.FADE_IN, 0.4f, 3f));
        yield return _waitForSeconds0_1;
        yield return StartCoroutine(FadeInText
            ("What do <color=yellow>you</color> think?", TEXT_APPEAR_TYPE.FADE_IN, 0.4f, 3f));
        yield return _waitForSeconds2;
        yield return StartCoroutine(FadeInText
            ("Welcome to <color=#00FFD5>Mint</color>'s research!", TEXT_APPEAR_TYPE.FADE_IN, 2f, 3f));
        yield return _waitForSeconds0_2;
        yield return StartCoroutine(FadeInText
            ("There's cookies on the table.", TEXT_APPEAR_TYPE.FADE_IN, 0.5f, 2f));
        yield return _waitForSeconds1_5;

        introText.color = new(0.024f, 0.769f, 0.647f);
        introText.fontStyle = FontStyles.Italic;
        yield return StartCoroutine(FadeInText
            ("\"Ah! Would you like to read with me? It's really comfortable sitting here.\"", TEXT_APPEAR_TYPE.TYPEWRITER, 3f, 5f));
        PlayerPrefs.SetInt("ResearchIntroPlayed", 1);
        SkillTree_Manager.enabled = true;
        Destroy(Overlay);
    }

    public enum TEXT_APPEAR_TYPE
    {
        FADE_IN,
        TYPEWRITER
    }
    IEnumerator FadeInText(string content, TEXT_APPEAR_TYPE type, float duration, float holdTime = 0f)
    {
        TMP_Text text = introText;
        text.text = "";

        float elapsed = 0f;
        Color originalColor = text.color;
        originalColor.a = 1f;

        if (type == TEXT_APPEAR_TYPE.FADE_IN)
        {
            text.text = content;
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                text.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
            text.color = originalColor;

        }
        else if (type == TEXT_APPEAR_TYPE.TYPEWRITER)
        {
            text.color = originalColor;
            text.text = "";
            float waitTime = duration / content.Length;
            foreach (char c in content)
            {
                text.text += c;
                yield return new WaitForSeconds(waitTime);
            }
        }

        yield return new WaitForSeconds(holdTime);

        elapsed = 0f;
        duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsed / duration);
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }
        text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        text.text = "";
    }
}
