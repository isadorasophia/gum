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

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 0 });
        }

        [TestMethod]
        public void TestNestedOneConditionAndConditionAndElseAndNestedIf()
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
            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 0, 15, 14 });
        }

        [TestMethod]
        public void TestElseAfterChoice()
        {
            const string situationText = @"
=Dinner
    (Socks >= 5)
        Do you hate socks!

        > Hell yeah!
            -> DestroyAll
        > Why would I?
            -> Sorry

    (...Socks > 1 and Socks < 5)
        thief: What about socks that you hate?
        Everything!!!!

    (...)
        - thief: Actually, some shoes are okay.
        Ew.

        - thief: Can you not look at me?
        What if I do.

        + Happy birthday! I bought you socks.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 4 });
        }

        [TestMethod]
        public void TestChoices()
        {
            const string situationText = @"
=Chitchat
    - You are amazing.
    FOR A COOKER.
    - I'm sorry. I was rude there.
    I needed to come up with stuff.
    I actually did enjoy your food.
    + ...
    - The dead fly was on purpose, right?";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 0 });

            Edge target = situation.Edges[0];

            Assert.AreEqual(target.Kind, RelationshipKind.HighestScore);
            CollectionAssert.AreEqual(target.Owners, new int[] { 0 });
            CollectionAssert.AreEqual(target.Blocks, new int[] { 1, 2, 3, 4 });
        }

        [TestMethod]
        public void TestNestedOneConditionAndOneConditionAndConditionAndElse()
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

    @1 (LookedAtLeft)
        -> Hey!?

    (!GotSword)
        (!Scared)
            What do you want?
    
        @1 (...)
            Please, stop.
            thief: Or what?

            -> Bye

    (...)
        -> Bye

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 2, 3, 8 });

            Edge target = situation.Edges[2];

            Assert.AreEqual(target.Kind, RelationshipKind.IfElse);
            CollectionAssert.AreEqual(target.Owners, new int[] { 2 });
            CollectionAssert.AreEqual(target.Blocks, new int[] { 0, 1 });

            target = situation.Edges[3];

            Assert.AreEqual(target.Kind, RelationshipKind.Next);
            CollectionAssert.AreEqual(target.Owners, new int[] { 3 });
            CollectionAssert.AreEqual(target.Blocks, new int[0]);

            target = situation.Edges[4];

            Assert.AreEqual(target.Kind, RelationshipKind.IfElse);
            CollectionAssert.AreEqual(target.Owners, new int[] { 4 });
            CollectionAssert.AreEqual(target.Blocks, new int[] { 5, 6 });

            target = situation.Edges[8];

            Assert.AreEqual(target.Kind, RelationshipKind.IfElse);
            CollectionAssert.AreEqual(target.Owners, new int[] { 8 });
            CollectionAssert.AreEqual(target.Blocks, new int[] { 4, 7 });
        }

        [TestMethod]
        public void TestChoicesWithCondition()
        {
            const string situationText = @"
=Choices
    - (!Eaten)
        I am FULL.
    - (!Eaten)
        I am soooooo stuffed.
    - (Hungry)
        DUDE I am hungry.
        [Eat=true]

=Bye";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 0 });

            Edge target = situation.Edges[0];

            Assert.AreEqual(target.Kind, RelationshipKind.HighestScore);
            CollectionAssert.AreEqual(target.Owners, new int[] { 0 });
            CollectionAssert.AreEqual(target.Blocks, new int[] { 1, 2, 3 });
        }


        [TestMethod]
        public void TestOneAndConditionAndOneWithConditionAndElseAndOneConditionAndGo()
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

    @1 (Left and StillHere)
        thief.happy: Uhhhhhhhhhhhh

        -> exit!

    // TODO: This is not being picked up in the root edges.
    // Maybe we need to add a root one and end NextNodes...
    -> Random

=Random";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            CollectionAssert.AreEqual(situation.NextBlocks, new int[] { 0, 3 });
        }
    }
}