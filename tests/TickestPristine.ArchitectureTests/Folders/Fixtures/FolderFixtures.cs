// Fixtures da regra de pastas: a pasta A usa uma regra da pasta B (violação), uma regra compartilhada, uma regra da
// própria pasta e as constantes da pasta B (permitidos).
namespace TickestPristine.ArchitectureTests.Folders.Fixtures
{
    internal static class FixtureNamespace
    {
        public const string Root = "TickestPristine.ArchitectureTests.Folders.Fixtures";
    }
}

namespace TickestPristine.ArchitectureTests.Folders.Fixtures.FolderA
{
    internal static class OwnFolderRule
    {
        public static bool Check(int value) => value > 0;
    }

    internal sealed class UsesOtherFolderRule
    {
        public static bool Run(int value) =>
            FolderB.OtherFolderRule.Check(value) &&
            Shared.SharedRule.Check(value) &&
            OwnFolderRule.Check(value) &&
            value != FolderB.OtherFolderConstants.Limit;
    }
}

namespace TickestPristine.ArchitectureTests.Folders.Fixtures.FolderB
{
    internal static class OtherFolderRule
    {
        public static bool Check(int value) => value < 100;
    }

    internal static class OtherFolderConstants
    {
        public const int Limit = 50;
    }
}

namespace TickestPristine.ArchitectureTests.Folders.Fixtures.Shared
{
    internal static class SharedRule
    {
        public static bool Check(int value) => value != 42;
    }
}
