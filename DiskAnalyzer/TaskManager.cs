namespace DiskAnalyzer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class TaskManager
    {
        private static readonly List<Task> tasks = [];
        private static readonly Lock _lock = new();

        public static bool IsAnyRunning => tasks.Count > 0;

        public static Task Run(Task task)
        {
            lock (_lock)
            {
                tasks.Add(task);
                task = task.ContinueWith(RemoveTask);
            }
            return task;
        }

        private static void RemoveTask(Task task)
        {
            lock (_lock)
            {
                tasks.Remove(task);
            }
        }
    }
}