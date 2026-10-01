using System.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;

namespace Top.Conversion.Tests.Pipeline
{
    public class ContentKindTests
    {
        [Test]
        public void AMapNeedsTheClientTables()
        {
            Assert.That(ContentKind.Requires(ContentKind.Map),
                Is.EqualTo(new[] { ContentKind.Table }));
        }

        [Test]
        public void AFamilyThatCanBeReadAloneNeedsNothing()
        {
            Assert.That(ContentKind.Requires(ContentKind.Character), Is.Empty);
            Assert.That(ContentKind.Requires(ContentKind.Item), Is.Empty);
            Assert.That(ContentKind.Requires(ContentKind.Scene), Is.Empty);
            Assert.That(ContentKind.Requires(ContentKind.Table), Is.Empty);
        }

        [Test]
        public void ExpandingAMapRunAddsItsTablesAfterIt()
        {
            Assert.That(ContentKind.Expand([ContentKind.Map]),
                Is.EqualTo(new[] { ContentKind.Map, ContentKind.Table }));
        }

        [Test]
        public void ExpandingNeverNamesAKindTwice()
        {
            Assert.That(ContentKind.Expand([ContentKind.Table, ContentKind.Map]),
                Is.EqualTo(new[] { ContentKind.Table, ContentKind.Map }));
        }

        [Test]
        public void ExpandingKeepsTheOrderItWasGiven()
        {
            Assert.That(ContentKind.Expand([ContentKind.Item, ContentKind.Map, ContentKind.Character]),
                Is.EqualTo(new[]
                {
                    ContentKind.Item, ContentKind.Map, ContentKind.Table, ContentKind.Character
                }));
        }

        [Test]
        public void ExpandingAWholeRunLeavesItAlone()
        {
            string[] every =
            [
                ContentKind.Character, ContentKind.Item, ContentKind.Scene, ContentKind.Table, ContentKind.Map
            ];

            Assert.That(ContentKind.Expand(every), Is.EqualTo(every));
        }

        [Test]
        public void ExpandingNothingIsEmpty()
        {
            Assert.That(ContentKind.Expand(Enumerable.Empty<string>()), Is.Empty);
        }
    }
}
