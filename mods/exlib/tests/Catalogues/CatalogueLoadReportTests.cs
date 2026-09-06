using System.Collections.Generic;
using ExpandedLib.Catalogues;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The summary every catalogue loader hands back from its <c>Load(ICoreAPI)</c>: what it read, what it
/// kept, and one line per thing it could not.
/// </summary>
public class CatalogueLoadReportTests {
  [Fact]
  public void Log_writes_one_summary_line_and_one_line_per_error() {
    var logger = Substitute.For<ILogger>();
    var report = new CatalogueLoadReport(
      "metals",
      3,
      12,
      ["iiex:config/metals/bad.json: unknown key 'thicknes'"]
    );

    report.Log(logger);

    logger
      .Received(1)
      .Notification(
        "[exlib] {0}: {1} file(s), {2} entr(ies), {3} error(s)",
        Arg.Is<object[]>(a =>
          a.Length == 4
          && (string)a[0] == "metals"
          && (int)a[1] == 3
          && (int)a[2] == 12
          && (int)a[3] == 1
        )
      );
    logger
      .Received(1)
      .Error("[exlib] iiex:config/metals/bad.json: unknown key 'thicknes'");
  }

  [Fact]
  public void A_report_with_no_errors_still_writes_its_summary() {
    var logger = Substitute.For<ILogger>();
    var report = new CatalogueLoadReport("liquids", 1, 4, []);

    report.Log(logger);

    logger
      .Received(1)
      .Notification(
        Arg.Any<string>(),
        Arg.Is<object[]>(a => a.Length == 4 && (int)a[3] == 0)
      );
    logger.DidNotReceive().Error(Arg.Any<string>());
  }
}
