using System.IO;
using System.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;

namespace Top.Conversion.Tests.Pipeline
{
    /// <summary>
    /// Discovery looks around a folder, so every test works inside its own
    /// throwaway tree and hands it a child to start from. That keeps the scan
    /// from reaching the temporary folder's neighbours, which belong to
    /// whatever else is running.
    /// </summary>
    public class ClientRootTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "top-client-root-tests",
                TestContext.CurrentContext.Test.ID);

            Delete();
        }

        [TearDown]
        public void TearDown()
        {
            Delete();
        }

        private string Near => Folder("work");

        [Test]
        public void AClientRootHoldsTablesBesideModels()
        {
            Assert.That(ClientRoot.Looks(Client("game/Client", maps: false)), Is.True);
        }

        [Test]
        public void AClientRootHoldsTablesBesideMaps()
        {
            Assert.That(ClientRoot.Looks(Client("game/Client", models: false)), Is.True);
        }

        [Test]
        public void AFolderWithOnlyTablesIsNotAClientRoot()
        {
            Folder("server/scripts/table");

            Assert.That(ClientRoot.Looks(Folder("server")), Is.False);
        }

        [Test]
        public void AFolderWithOnlyModelsIsNotAClientRoot()
        {
            Folder("loose/model/character");

            Assert.That(ClientRoot.Looks(Folder("loose")), Is.False);
        }

        [Test]
        public void AMissingFolderIsNotAClientRoot()
        {
            Assert.That(ClientRoot.Looks(Path.Combine(_root, "nowhere")), Is.False);
            Assert.That(ClientRoot.Looks(null), Is.False);
            Assert.That(ClientRoot.Looks("  "), Is.False);
        }

        [Test]
        public void CandidatesFindAClientCheckedOutBesideTheProject()
        {
            var client = Client("game/Client");

            Assert.That(ClientRoot.Candidates(Near), Is.EqualTo(new[] { client }));
        }

        [Test]
        public void CandidatesOfferTheFolderItselfFirst()
        {
            var client = Client("client");

            Assert.That(ClientRoot.Candidates(client), Is.EqualTo(new[] { client }));
        }

        [Test]
        public void CandidatesIgnoreFoldersThatAreNotClients()
        {
            Folder("project/src");
            Folder("project/tests");
            Client("game/Client");

            var found = ClientRoot.Candidates(Near);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found.Single(), Does.EndWith(Path.Combine("game", "Client")));
        }

        [Test]
        public void CandidatesFindTheReferenceAssetsDefault()
        {
            var assets = Client("reference/assets");

            Assert.That(ClientRoot.Candidates(Near), Is.EqualTo(new[] { assets }));
        }

        [Test]
        public void CandidatesFindAClientNestedOneLevelDown()
        {
            Client("dist/Client");

            Assert.That(ClientRoot.Candidates(Near), Has.Count.EqualTo(1));
        }

        [Test]
        public void CandidatesAreEmptyWhenNothingNearIsAClient()
        {
            Folder("project/src");

            Assert.That(ClientRoot.Candidates(Near), Is.Empty);
        }

        [Test]
        public void DescribeCountsWhatTheClientHolds()
        {
            var client = Client("game/Client");

            File.WriteAllText(Path.Combine(client, "map", "garner.map"), string.Empty);
            File.WriteAllText(Path.Combine(client, "map", "magicsea.map"), string.Empty);
            File.WriteAllText(Path.Combine(client, "model", "character", "0001.lgo"), string.Empty);

            Assert.That(ClientRoot.Describe(client), Is.EqualTo(
                "2 maps, 1 character parts, 0 item modules"));
        }

        private string Client(string relative, bool models = true, bool maps = true)
        {
            var client = Folder(relative);

            Folder(Path.Combine(relative, "scripts", "table"));
            Directory.CreateDirectory(Path.Combine(client, "animation"));

            if (models)
            {
                Directory.CreateDirectory(Path.Combine(client, "model", "character"));
                Directory.CreateDirectory(Path.Combine(client, "model", "item"));
            }

            if (maps)
            {
                Directory.CreateDirectory(Path.Combine(client, "map"));
            }

            return client;
        }

        private string Folder(string relative)
        {
            var path = Path.GetFullPath(Path.Combine(_root, relative));

            Directory.CreateDirectory(path);

            return path;
        }

        private void Delete()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
