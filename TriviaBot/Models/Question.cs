using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriviaBot.Models
{
    public class Question
    {
        public Guid QuestionId { get; set; }
        public string Statement { get; set; }
        public DateTime Date { get; set; }
        public List<Answer> Answers { get; set; }
        public List<Attempt> Attempts { get; set; }
    }
}
