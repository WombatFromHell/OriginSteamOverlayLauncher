using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OriginSteamOverlayLauncher;

namespace OSOL.UnitTests
{
    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void CriticalConfigVars_Valid_ReturnTrue()
        {
            Settings Data = new Settings(testable: true);
            var result = Data.KeyExists("GamePath", testable: true) &&
                Data.KeyExists("LauncherPath", testable: true);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ConfigMatchesInstance_ReturnTrue()
        {
            Settings Data = new Settings(testable: true);
            var result = Data.CompareKeysToProps(testable: true, instance: Data);
            Assert.IsTrue(result);
        }
    }
}
