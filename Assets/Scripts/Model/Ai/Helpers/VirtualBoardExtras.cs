#nullable enable


namespace AI.Helpers.VirtualBoard
{
    public struct ActiveVirtualBoard<T> where T : class
    {
        public VirtualBoard<T> VirtualBoard;

        private ActiveVirtualBoard(VirtualBoard<T> virtualBoard) {
            VirtualBoard = virtualBoard;
        }

        public static explicit operator VirtualBoard<T>(ActiveVirtualBoard<T> activeVirtualBoard)
        {
            return activeVirtualBoard.VirtualBoard;
        }

        public static explicit operator ActiveVirtualBoard<T>(VirtualBoard<T> virtualBoard)
        {
            return new ActiveVirtualBoard<T>(virtualBoard);
        }
    }

    public class VirtualBoardNoData : VirtualBoard<VirtualBoardManager.EmptyClass>
    {
        public void UpdateToReal()
        {
            UpdateToReal(a => new());
        }
    }

    public struct ActiveVirtualBoardNoData
    {
        public VirtualBoardNoData VirtualBoard;

        private ActiveVirtualBoardNoData(VirtualBoardNoData virtualBoard) {
            VirtualBoard = virtualBoard;
        }

        public static explicit operator VirtualBoardNoData(ActiveVirtualBoardNoData activeVirtualBoard)
        {
            return activeVirtualBoard.VirtualBoard;
        }

        public static explicit operator ActiveVirtualBoardNoData(VirtualBoardNoData virtualBoard)
        {
            return new ActiveVirtualBoardNoData(virtualBoard);
        }
    }
}