using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Team8.Core;

namespace Team8.Tests
{
    public class CsvTests
    {
        [Test]
        public void NumbersUseDotEvenInGermanLocale()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("1.500", CsvUtil.Num(1.5));
                Assert.AreEqual("12.3", CsvUtil.Num(12.34, 1));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = old;
            }
        }

        [Test]
        public void NullNumberIsBlank()
        {
            Assert.AreEqual("", CsvUtil.Num((double?)null));
        }

        [Test]
        public void FieldsWithCommasGetQuoted()
        {
            Assert.AreEqual("a,\"b,c\",\"say \"\"hi\"\"\"", CsvUtil.Row("a", "b,c", "say \"hi\""));
        }

        [Test]
        public void TrialRowMatchesHeader()
        {
            var r = new TrialResult
            {
                Participant = "P01", LatinRow = 1, Trial = 2, Condition = Condition.Trail, Layout = 'B',
                CompletionSeconds = 300, TimedOut = true, AppVersion = "0.1.0"
            };
            r.LeverSeconds[0] = 41.25;
            var fields = r.ToCsvFields();
            Assert.AreEqual(CsvSchema.TrialColumns.Length, fields.Length);
            Assert.AreEqual("trail", fields[3]);
            Assert.AreEqual("300.000", fields[5]);
            Assert.AreEqual("1", fields[6]);
            Assert.AreEqual("41.250", fields[8]);
            Assert.AreEqual("", fields[9]);
        }
    }
}
