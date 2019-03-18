// using System;
// using System.IO;
// using System.Threading;
// using System.Threading.Tasks;

// namespace NnManager {
//     using RPath = Util.RestrictedPath;

//     partial class NnTask {
//         NnModule Test {
//             get {
//                 return new NnModule(
//                     TestExecute,
//                     TestCanExecute,
//                     () => true);
//             }
//         }

//         bool TestCanExecute() {
//             return true;
//         }

//         bool TestExecute(CancellationToken ct) {
//             RPath logFilePath = path.SubPath("test").SubPath("log.txt");

//             Random random = new System.Random(
//                 Interlocked.Increment(ref Util.seed)
//             );

//             StartLogParser<TestLog>(logFilePath, ct);

//             StreamWriter sw = new StreamWriter(
//                 new FileStream(
//                     logFilePath,
//                     FileMode.OpenOrCreate,
//                     FileAccess.Write,
//                     FileShare.Read
//                 )
//             );
//             for (int i = 0; i < 10; ++i) {
//                 if (ct.IsCancellationRequested)
//                     break;
//                 sw.WriteLine(Util.RandomString(20));
//                 sw.Flush();
//                 try {
//                     System.Threading.Tasks.Task.Delay(
//                         random.Next(100, 2000)
//                     ).Wait(ct);
//                 } catch { }
//             }
//             sw.Close();

//             StopLogParser();

//             return true;
//         }

//         class TestLog : LogBase {
//             public TestLog() { }
//             public TestLog(Action<string> setStatus) : base(setStatus) { }

//             enum TestRunStatus {
//                 Idle,
//                 Odd,
//                 Even
//             }

//             TestRunStatus status = TestRunStatus.Idle;
//             int lineCount = 0;

//             public override void Push(string line) {

//                 lineCount++;

//                 switch (status) {
//                     case TestRunStatus.Idle:
//                         status = TestRunStatus.Odd;
//                         break;

//                     case TestRunStatus.Odd:
//                         status = TestRunStatus.Even;
//                         break;

//                     case TestRunStatus.Even:
//                         status = TestRunStatus.Odd;
//                         break;
//                 }

//                 switch (status) {
//                     case TestRunStatus.Idle:
//                         SetStatus("Idle" + lineCount.ToString());
//                         break;

//                     case TestRunStatus.Odd:
//                         SetStatus("Odd" + lineCount.ToString());
//                         break;

//                     case TestRunStatus.Even:
//                         SetStatus("Even" + lineCount.ToString());
//                         break;
//                 }
//             }
//         }
//     }
// }