using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests.Acid
{
    [TestClass]
    public class StringLogic
    {
        [TestMethod]
        public void AcidStringLogic()
        {
            var (ms, _) = Test(new[] {
                    $"num=1 if \"\" then goto 19 end num++",
                    "if \"abc\" then goto 19 end num++",
                    "if \"1\" then goto 19 end num++",
                    "if \"0\" then goto 19 end num++",
                    "if not \"\" then goto 19 end num++",
                    "if not \"1\" then goto 19 end num++",
                    "if not \"0\" then goto 19 end num++",
                    "if 1 and \"\" then goto 19 end num++",
                    "if 1 and \"1\" then goto 19 end num++",
                    "if 1 and \"0\" then goto 19 end num++",
                    "if not (1 or \"\") then goto 19 end num++",
                    "if not (1 or \"1\") then goto 19 end num++",
                    "if not (1 or \"0\") then goto 19 end num++",
                    "if 0 or \"\" then goto 19 end num++",
                    "if 0 or \"1\" then goto 19 end num++",
                    "if 0 or \"0\" then goto 19 end num++",
                    "if num != 17 then OUTPUT=\"Skipped: \"+(17-num)+\" tests\" goto 20 end",
                    "OUTPUT=\"ok\" goto20",
                    "output=\"Failed test #\"+num",
                    "goto20"
                },
                30
            );

            Assert.AreEqual("ok", ms.GetVariable("OUTPUT").String.ToString());
        }
    }
}
