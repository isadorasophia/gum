using Gum.InnerThoughts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Gum.Tests
{
    [TestClass]
    public class Bungee
    {
        private CharacterScript? Read(string input)
        {
            string[] lines = Regex.Split(input, @"\r?\n|\r");
            Parser parser = new("Test", lines);

            return parser.Start();
        }

        [TestMethod]
        public void TestSerialization()
        {
            const string path = "./resources";

            CharacterScript[] results = Reader.Parse(path, lastModified: null, out string errors);

            Assert.IsTrue(string.IsNullOrEmpty(errors));
            Assert.AreEqual(1, results.Length);

            IEnumerable<Situation> situations = results[0].FetchAllSituations();
            Assert.AreEqual(3, situations.Count());
        }

        [TestMethod]
        public void TestDeserialization()
        {
            const string path = "./resources";

            CharacterScript[] results = Reader.Parse(path, lastModified: null, out string errors);

            Assert.IsTrue(string.IsNullOrEmpty(errors));
            Assert.AreEqual(1, results.Length);

            CharacterScript script = results[0];
            string json = JsonConvert.SerializeObject(script, Reader.Settings);

            CharacterScript? deserializedScript = JsonConvert.DeserializeObject<CharacterScript>(json);

            Assert.IsNotNull(deserializedScript);

            IEnumerable<Situation> situations = deserializedScript.FetchAllSituations();
            Assert.AreEqual(3, situations.Count());
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
        public void TestSingleCondition()
        {
            const string situationText = @"
=Encounter
    (!HasSeenThis)
        Wow! Have you seen this?";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Block block = situation.Blocks[1];

            Assert.AreEqual(1, block.Requirements.Count);
            Assert.AreEqual(CriterionNodeKind.And, block.Requirements[0].Kind);
            Assert.AreEqual(CriterionKind.Different, block.Requirements[0].Criterion.Kind);
            Assert.AreEqual(true, block.Requirements[0].Criterion.BoolValue);
        }

        [TestMethod]
        public void TestSingleSentenceWithSpeaker()
        {
            const string situationText = @"
=Encounter
    speaker.happy: I just have one sentence.";

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
        public void TestOrderChoice()
        {
            const string situationText = @"
=Encounter
    @order
        - Hello
        + Bye!

    -> exit!";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoicesWithRules()
        {
            const string situationText = @"
=Encounter
    - (HasIceCream)
        This seems a pretty good ice cream.
    - (!HasIceCream)
        What do you have there??
    - (WithoutCoins and HasIceCream)
        Maybe you want to sell that ice cream of yours?";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(target.Owner, 0);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, target.Blocks);

            Block block = situation.Blocks[1];
            Assert.AreEqual(1, block.Requirements.Count);
            Assert.AreEqual(1, block.Lines.Count);

            block = situation.Blocks[2];
            Assert.AreEqual(1, block.Requirements.Count);
            Assert.AreEqual(1, block.Lines.Count);

            block = situation.Blocks[3];
            Assert.AreEqual(2, block.Requirements.Count);
            Assert.AreEqual(1, block.Lines.Count);
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
            CollectionAssert.AreEqual(new int[] { 1, 7 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);
        }

        [TestMethod]
        public void TestOptionsWithChoices()
        {
            const string situationText = @"
=Choice
    +   >> Settle down for a while?
        > Rest my eyes...
            [c:SaveCheckpointInteraction]
        > Keep going.

    +   >> Do you want it all to be all right?
        > Just for a while.
            [c:SaveCheckpointInteraction]
        > Not now.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 6 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7 }, target.Blocks);

            target = situation.Edges[7];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(7, target.Owner);
            CollectionAssert.AreEqual(new int[] { 8, 10 }, target.Blocks);

            target = situation.Edges[8];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(8, target.Owner);
            CollectionAssert.AreEqual(new int[] { 9 }, target.Blocks);
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
            CollectionAssert.AreEqual(new int[] { 1, 7 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoiceWithAction()
        {
            const string situationText = @"
=Dinner
    >> Hate socks?
    > Hell yeah!
        [FireOnSocks=true]
    > Why would I?
        [c:GoAwayInteraction]";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 4 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoiceWithNestedBlock()
        {
            const string situationText = @"
=Dinner
    >> Hate socks?
    > Hell yeah!
        (LookForFire)
            Yes...?
    > Why would I?
        No!";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 4 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3 }, target.Blocks);

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

        +   Happy birthday! I bought you socks.

= DestroyAll

= Sorry";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 7, 8 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[8];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(8, target.Owner);
            CollectionAssert.AreEqual(new int[] { 9, 10, 11 }, target.Blocks);
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

=Bye
=Hey!?";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(situation.Root, 0);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 4, 11 }, target.Blocks);

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

        [TestMethod]
        public void TestNestedCondition3()
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
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 6, 8 }, target.Blocks);

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

        [TestMethod]
        public void TestNestedCondition6()
        {
            const string situationText = @"
=Encounter
    @1  (Defeated)
            Okay. I am here now.
            Right?

            [c:DoSomething]
        
        (...)
            Oh my. Congratulations.

            (WonInThePast)
                I don't care though.
            (...)
                I am super jealous.

            I hope you like the taste of victory.
    
    -> Random

=Random";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 7 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4, 5 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition7()
        {
            const string situationText = @"
=Encounter
    (Meet and PreviouslySaidBye)
        @1  Hi! But only once.

            -> exit!

        (DatesTogether >= 1)
            I guess this is really like meeting again...?

    (...!StillHasJob)
        (ChocolateAmount == 0)
            @1  Out of chocolates?
                Again...?

        (...ChocolateAmount >= 1)
            - I mean, chocolates are great!

            - Until they aren't.
    (...)
        @1  I am embarassed.
            Bye.

=Random";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 4, 10 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5, 7 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[7];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(7, target.Owner);
            CollectionAssert.AreEqual(new int[] { 8, 9 }, target.Blocks);

            target = situation.Edges[10];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(10, target.Owner);
            CollectionAssert.AreEqual(new int[] { 11 }, target.Blocks);
        }

        [TestMethod]
        public void TestNestedCondition8()
        {
            const string situationText = @"
=Encounter
    (!Meet and !Greet) 
        @order
            -   I will go now.
            +   Hello!?

        -> exit!";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            // This edge currently has an extra edge to 4.
            // This technically doesn't make sense because 1 will always be chosen.
            // I *think* we could do some fancy inference around leaves? But for now,
            // I will leave it like this.
            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 4 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
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
        public void TestNestedCondition9()
        {
            const string situationText = @"
=MapThree
    -   mushroom: ...
        He says hi.

    -   (!UnlockedBoatTravel)
            You seem more lost than usual.

        (TalkedWithBoatman)
            You met the boatman, haven't you?
            He must know a way to get where you want.

        (...)
            You can try searching for the boatman. He knows his way around here.
            I was told he is somewhere in the glimmering forest.
    
    -   I used to be very upset about the farmlands.
        I did not like it here. The smell. The endlessness.
        The overbearing wall.
        I came to accept it, eventually.
        And it became easier.

    -> Choice

=Choice";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.HighestScore, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2, 5 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 4 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);
        }


        [TestMethod]
        public void TestLinearOneBlocks()
        {
            const string situationText = @"
=CreateVillage
    @1  -> ChooseName

    @1  Seriously? You are not living in a place called {VillageName}.
        Choose an actual name now:
        -> exit!

    @1  (HasChosenSameName)
            Okay, I guess {VillageName} is what YOU really want.
        
    Move to {VilageName}?

    >> Ready?
    > Yes.
        -> ChooseName
    > No.
        -> ChooseName

=ChooseName";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 7, 9 }, target.Blocks);

            target = situation.Edges[7];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(7, target.Owner);
            CollectionAssert.AreEqual(new int[] { 8 }, target.Blocks);

            target = situation.Edges[9];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(9, target.Owner);
            CollectionAssert.AreEqual(new int[] { 10 }, target.Blocks);
        }

        [TestMethod]
        public void TestConditionWithOneJoin()
        {
            const string situationText = @"
=Encounter
    (Something)
        @1  Hello!

    (...Something2)
        @1  Hello once?

    Bye!

=ChooseName";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(6, situation.Root);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 3 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            Assert.AreEqual(1, situation.Blocks[2].PlayUntil); // @1

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            Assert.AreEqual(1, situation.Blocks[4].PlayUntil); // @1

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 0, 5 }, target.Blocks);
        }

        [TestMethod]
        public void TestConditionWithTwoLinesJoin()
        {
            const string situationText = @"
=Encounter
    (Something)
        @1  Hello!
            ok...

    (...Something2)
        @1  Hello once?

    Bye!

=ChooseName";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(6, situation.Root);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 3 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            Assert.AreEqual(1, situation.Blocks[2].PlayUntil); // @1

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            Assert.AreEqual(1, situation.Blocks[4].PlayUntil); // @1

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

            target = situation.Edges[6];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(6, target.Owner);
            CollectionAssert.AreEqual(new int[] { 0, 5 }, target.Blocks);
        }

        [TestMethod]
        public void TestConditionWithOneIfElseJoin()
        {
            const string situationText = @"
=Encounter
    @1  (Something)
            Hello!

        (...Something2)
            Hello once?

    Bye!

=ChooseName";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(0, situation.Root);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 5 }, target.Blocks);

            Assert.AreEqual(1, situation.Blocks[1].PlayUntil); // @1

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.IfElse, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2, 3, 4 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 5 }, target.Blocks);

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
        public void TestConditionWithQuestion()
        {
            const string situationText = @"
=Encounter
    (TriedPickName > 3)
        >> Do you really think a name will stop you from running away?
        > Yes.
            -> ChooseName
        > No.
            -> Encounter

=ChooseName";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(0, situation.Root);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3, 5 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);
        }

        [TestMethod]
        public void TestChoiceInsideIf()
        {
            const string situationText = @"
=Encounter
    (SupposedlyHaveAJob)
        Especially since you don't even have a job.

        >> No job...
        > All my paperwork is set, sir.
        > I am very tired and I just want to go home.

    Okay.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(0, situation.Root);

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
        public void TestChoiceOutsideIf()
        {
            const string situationText = @"
=Encounter
    (SupposedlyHaveAJob)
        Especially since you don't even have a job.

    >> No job...
    > All my paperwork is set, sir.
    > I am very tired and I just want to go home.

    Okay.";

            CharacterScript? script = Read(situationText);
            Assert.IsTrue(script != null);

            Situation? situation = script.FetchSituation(id: 0);
            Assert.IsTrue(situation != null);

            Assert.AreEqual(0, situation.Root);

            Edge target = situation.Edges[0];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(0, target.Owner);
            CollectionAssert.AreEqual(new int[] { 1, 2 }, target.Blocks);

            target = situation.Edges[1];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(1, target.Owner);
            CollectionAssert.AreEqual(new int[] { 2 }, target.Blocks);

            target = situation.Edges[2];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(2, target.Owner);
            CollectionAssert.AreEqual(new int[] { 3 }, target.Blocks);

            target = situation.Edges[3];

            Assert.AreEqual(EdgeKind.Choice, target.Kind);
            Assert.AreEqual(3, target.Owner);
            CollectionAssert.AreEqual(new int[] { 4, 5 }, target.Blocks);

            target = situation.Edges[4];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(4, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

            target = situation.Edges[5];

            Assert.AreEqual(EdgeKind.Next, target.Kind);
            Assert.AreEqual(5, target.Owner);
            CollectionAssert.AreEqual(new int[] { 6 }, target.Blocks);

        }
    }
}