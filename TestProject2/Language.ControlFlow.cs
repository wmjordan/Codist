using TestProject;

namespace TestProject2.Language.ControlFlow;

[ApiVersion(15)]
internal class LabeledBreakOrContinue
{
	void Test() {
		int[,] grid = {
			{ 0, 0, 0 },
			{ 0, 1, 0 },
			{ 0, 0, 2 }
		};

		int foundRow = -1;
		int foundColumn = -1;
		int processed = 0;

	outer:
		for (int row = 0; row < grid.GetLength(0); row++) {
			for (int column = 0; column < grid.GetLength(1); column++) {
				int cell = grid[row, column];

				if (cell == 1) {
					// continue to next row
					continue outer;
				}

				if (cell == 2) {
					foundRow = row;
					foundColumn = column;

					// exit all loops
					break outer;
				}

				processed++;
			}
		}

	}
}
