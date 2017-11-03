using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriviaBot.Models
{
    public class Attempt
    {
        public Guid AttemptId { get; set; }
        public Question Question { get; set; }
        public Player Player { get; set; }
        public Guid PlayerId { get; set; }
        public Answer Answer { get; set; }
        public bool Correct { get; set; }
    }
}
