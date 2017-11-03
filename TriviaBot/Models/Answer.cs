using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriviaBot.Models
{
    public class Answer
    {
        public Guid AnswerId { get; set; }
        public string Statement { get; set; }
        public Question Question { get; set; }
        public bool IsCorrect { get; set; }
    }
}
