using Gum.InnerThoughts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Gum.Tests
{
    [TestClass]
    public class Bungee
    {
        private CharacterScript? Read(string input)
        {
            string[] lines = Regex.Split(input, @"\r?\n|\r");
            Parser parser = new(lines);

            return parser.Start();
        }

        [TestMethod]
        public void TestSingleSentence()
        {
            const string situationText = @"
=Encounter
    I just have one sentence.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(target.Owner, 0);
            CollectionAssert.AreEqual(new int[] { 1 }, target.Blocks);
        }

        [TestMethod]
        public void TestSimpleIf()
        {
            const string situationText = @"
=Encounter
    (LikeFishes)
        Wow, I love fishes!

    (...)
        Ugh, I hate fishes.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2 }, target.Blocks);
        }

        [TestMethod]
        public void TestLineAfterChoice()
        {
            const string situationText = @"
=Dinner
    (Socks >= 5)
        Do you hate socks!

        >> Hate socks?
            > Hell yeah!
                -> exit!
            > Why would I?
                -> exit!

    Okay, bye!";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 5 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 4 }, target.Blocks);
        }

        [TestMethod]
        public void TestLineAfterChoiceWithoutLeaves()
        {
            const string situationText = @"
=Dinner
    (Socks >= 5)
        Do you hate socks!

        >> Hate socks?
            > Hell yeah!
                Okay.
            > Why would I?
                Yeah??

    Okay, bye!";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 5 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 4 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);
        }

        [TestMethod]
        public void TestElseAfterChoice()
        {
            const string situationText = @"
=Dinner
    (Socks >= 5)
        Do you hate socks!

        >> Hate socks?
            > Hell yeah!
                -> DestroyAll
            > Why would I?
                -> Sorry

    (...Socks > 1 and Socks < 5)
        thief: What about socks that you hate?
        Everything!!!!

    (...)
        -   thief: Actually, some shoes are okay.
            Ew.

        -   thief: Can you not look at me?
            What if I do.

        +   Happy birthday! I bought you socks.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 5, 6 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 4 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7, 8, 9 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoices()
        {
            const string situationText = @"
=Chitchat
    -   You are amazing.
        FOR A COOKER.
    -   I'm sorry. I was rude there.
        I needed to come up with stuff.
        I actually did enjoy your food.
    +   ...
    -   The dead fly was on purpose, right?";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoicesWithCondition()
        {
            const string situationText = @"
=Choices
    -   (!Eaten)
            I am FULL.
    -   (!Eaten)
            I am soooooo stuffed.
    -   (Hungry)
            DUDE I am hungry.
            [Eat=true]

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition1()
        {
            const string situationText = @"
=Encounter
    @1  (!SawMe)
            Do you see anything?
            Of course not.

            [Variable=true]

            -> exit!

        (...)
            Okay you see me.
            I'm sorry.
            thief: For what?
            thief: I am simply existing here.

            -> Bye

    @1  (LookedAtLeft)
            -> Hey!?

    (!GotSword)
        (!Scared)
            What do you want?
    
        (...)
            @1  Please, stop.
                thief: Or what?

                -> Bye

    (...)
        -> Bye

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(situation.Root, 0);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 4, 5, 11 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7, 8 }, target.Blocks);

            target = situation.Edges[8];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(8, target.Owner);
            CollectionAssert.AreEqual(new int[] { 9 }, target.Blocks);

            target = situation.Edges[11];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(11, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6, 10 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition2()
        {
            const string situationText = @"
=Welcome
    @1  1!
        2

        (Three)
            4
            5

            -> Bye

        (...)
            6
            7
        
        [Condition=true]
        [c:Interaction]

        -> exit!

    (!Eight)
        9
        10 {i:Variable}!

        -> Bye
    
    (...!Eleven)
        (Twelve)
            @random
                + 13
                + 14

        (...)
            @random
                + 15
                ...
                16
                + 17

        -> exit!

    -> Bye

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(situation.Name, "Welcome");

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 16 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 8, 11 }, target.Blocks);

            target = situation.Edges[7];

            Assert.AreEqual(target.Kind, EdgeKind.IfElse);
            Assert.AreEqual(target.Owner, 7);
            CollectionAssert.AreEqual(new int[] { 5, 6 }, target.Blocks);

            target = situation.Edges[8];

            Assert.AreEqual(EdgeKind.Random, target.Kind);
            Assert.AreEqual(8, target.Owner);
            CollectionAssert.AreEqual(new int[] { 9, 10 }, target.Blocks);

            target = situation.Edges[9];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(9, target.Owner);
            CollectionAssert.AreEqual(new int[] { 14 }, target.Blocks);

            target = situation.Edges[10];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(10, target.Owner);
            CollectionAssert.AreEqual(new int[] { 14 }, target.Blocks);

            target = situation.Edges[11];

            Assert.AreEqual(EdgeKind.Random, target.Kind);
            Assert.AreEqual(11, target.Owner);
            CollectionAssert.AreEqual(new int[] { 12, 13 }, target.Blocks);

            target = situation.Edges[12];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(12, target.Owner);
            CollectionAssert.AreEqual(new int[] { 14 }, target.Blocks);

            target = situation.Edges[13];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(13, target.Owner);
            CollectionAssert.AreEqual(new int[] { 14 }, target.Blocks);

            target = situation.Edges[16];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(16, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7, 15 }, target.Blocks);
        }

        [TestMethod] public void TestNestedCondition3()
        {
            const string situationText = @"
=Encounter
    @1  (!SawMe)
            Do you see anything?
            Of course not.

            [Variable=true]
        (...)
            Okay you see me.
            I'm sorry.
            thief: For what?
            thief: I am simply existing here.

    Bye.

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 4 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition4()
        {
            const string situationText = @"
=Encounter
    @1  (!SawMe)
            Do you see anything?
            Of course not.

            [Variable=true]

        (...)
            Okay you see me.
            I'm sorry.
            thief: For what?
            thief: I am simply existing here.

        I guess?

    Bye.

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 5 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition5()
        {
            const string situationText = @"
=Encounter
    @1  
        Hello.
        I do talk a lot.
        \(you might have noticed?\)
        SO. As I was saying-
        thief: What?
        Nevermind.

    (Defeated)
        WOW!! You did it!
        -> exit!

    // this will think that it's joining with the one above ^
    @1  
        Can you go now?

        (!CanMove)
            thief: I would. If I wasn't STUCK.
            Wow wow wow! Hold on!
            What do you mean STUCK?
            thief: Stuck!
            Okay.

        (...)
            thief.happy: Yes.
            So go!
            Maybe I will!
            Okay.
            Okay.

            [Left=true]
            -> exit!

    @1  (Left and StillHere)
            thief.happy: Uhhhhhhhhhhhh

            -> exit!

    -> Random

=Random";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 6, 7, 8 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4, 5 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);
        }
    }
}