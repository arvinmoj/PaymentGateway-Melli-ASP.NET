using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cart_Exam.Data;
using Microsoft.AspNetCore.Mvc;
using Cart_Exam.Models;
using Cart_Exam.Models.ViewModels;
using Newtonsoft.Json;

namespace Cart_Exam.Controllers
{
    public class HomeController : Controller
    {
        private MyContext _ctx;

        public HomeController(MyContext ctx)
        {
            _ctx = ctx;
        }

        public IActionResult Index()
        {
            return View(_ctx.Products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult CallBack(CallbackRequestPayment result)
        {
            var order = _ctx.Orders.FirstOrDefault(o => o.OrderId == result.OrderId);
            if (order == null)
            {
                return NotFound();
            }

            string merchantId = "000000140212149";
            string terminalId = "24000615";
            string merchantKey = "kLheA+FS7MLoLlLVESE3v3/FP07uLaRw";

            var byteData = Encoding.UTF8.GetBytes(result.Token);

            var algorithm = SymmetricAlgorithm.Create("TripleDes");
            algorithm.Mode = CipherMode.ECB;
            algorithm.Padding = PaddingMode.PKCS7;

            var encryptor = algorithm.CreateEncryptor(Convert.FromBase64String(merchantKey), new byte[8]);
            string signData = Convert.ToBase64String(encryptor.TransformFinalBlock(byteData, 0, byteData.Length));

            var data = new
            {
                Token = result.Token,
                SignData = signData
            };

            var verifyRes = CallApi<VerifyResultData>("https://sadad.shaparak.ir/api/v0/Advice/Verify", data).Result;
            if (verifyRes.ResCode == 0)
            {
                order.IsFinaly = true;

                _ctx.Orders.Update(order);
                _ctx.SaveChanges();

                return View("SuccessPaymentView", verifyRes);
            }
            else
            {
                return View("ErrorPaymentView", verifyRes);
            }


            return Content("");
        }

        public async Task<T> CallApi<T>(string apiUrl, object value) where T : new()
        {
            using (var client = new HttpClient())
            {

                client.BaseAddress = new Uri(apiUrl);
                client.DefaultRequestHeaders.Accept.Clear();

                var json = JsonConvert.SerializeObject(value);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var w = client.PostAsync(apiUrl, content);
                w.Wait();

                HttpResponseMessage response = w.Result;
                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadAsStringAsync();
                    result.Wait();
                    return JsonConvert.DeserializeObject<T>(result.Result);
                }

                return new T();
            }
        }

    }
}
