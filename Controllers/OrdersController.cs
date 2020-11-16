using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cart_Exam.Data;
using Cart_Exam.Models;
using Cart_Exam.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Cart_Exam.Controllers
{
    public class OrdersController : Controller
    {
        private MyContext _ctx;

        public OrdersController(MyContext ctx)
        {
            _ctx = ctx;
        }
        public IActionResult AddToCart(int id)
        {

            Order order = _ctx.Orders.SingleOrDefault(o => !o.IsFinaly);
            if (order == null)
            {
                order = new Order()
                {
                    CreateDate = DateTime.Now,
                    IsFinaly = false,
                    Sum = 0
                };
                _ctx.Orders.Add(order);
                _ctx.OrderDetails.Add(new OrderDetail()
                {
                    OrderId = order.OrderId,
                    Count = 1,
                    Price = _ctx.Products.Find(id).Price,
                    ProductId = id
                });
                _ctx.SaveChanges();
            }
            else
            {
                var details = _ctx.OrderDetails.SingleOrDefault(d => d.OrderId == order.OrderId && d.ProductId == id);
                if (details == null)
                {
                    _ctx.OrderDetails.Add(new OrderDetail()
                    {
                        OrderId = order.OrderId,
                        Count = 1,
                        Price = _ctx.Products.Find(id).Price,
                        ProductId = id
                    });
                }
                else
                {
                    details.Count += 1;
                    _ctx.Update(details);
                }

                _ctx.SaveChanges();
            }
            UpdateSumOrder(order.OrderId);
            return Redirect("/");
        }

        public IActionResult ShowOrder()
        {

            Order order = _ctx.Orders.SingleOrDefault(o => !o.IsFinaly);

            List<ShowOrderViewModel> _list = new List<ShowOrderViewModel>();
            if (order != null)
            {
                var details = _ctx.OrderDetails.Where(d => d.OrderId == order.OrderId).ToList();
                foreach (var item in details)
                {
                    var product = _ctx.Products.Find(item.ProductId);

                    _list.Add(new ShowOrderViewModel()
                    {
                        Count = item.Count,
                        ImageName = product.ImageName,
                        OrderDetailId = item.OrderDetailID,
                        Price = item.Price,
                        Sum = item.Count * item.Price,
                        Title = product.Title
                    });

                }
            }

            return View(_list);
        }

        public IActionResult Delete(int id)
        {
            var orderDetail = _ctx.OrderDetails.Find(id);
            _ctx.Remove(orderDetail);
            _ctx.SaveChanges();
            return RedirectToAction("ShowOrder");
        }

        public IActionResult Command(int id, string command)
        {
            var orderDetail = _ctx.OrderDetails.Find(id);

            switch (command)
            {
                case "up":
                    {
                        orderDetail.Count += 1;
                        _ctx.Update(orderDetail);
                        break;
                    }
                case "down":
                    {
                        orderDetail.Count -= 1;
                        if (orderDetail.Count == 0)
                        {
                            _ctx.OrderDetails.Remove(orderDetail);
                        }
                        else
                        {
                            _ctx.Update(orderDetail);
                        }

                        break;
                    }
            }


            _ctx.SaveChanges();
            return RedirectToAction("ShowOrder");
        }
        public void UpdateSumOrder(int orderId)
        {
            var order = _ctx.Orders.Find(orderId);
            order.Sum = _ctx.OrderDetails.Where(o => o.OrderId == order.OrderId).Select(d => d.Count * d.Price).Sum();
            _ctx.Update(order);
            _ctx.SaveChanges();
        }

        public IActionResult Payment()
        {
            var order = _ctx.Orders.FirstOrDefault(o => !o.IsFinaly);
            if (order==null)
            {
                return NotFound();
            }

            string merchantId = "000000140212149";
            string terminalId = "24000615";
            string merchantKey = "kLheA+FS7MLoLlLVESE3v3/FP07uLaRw";

            string signDataBeforeEncode = $"{terminalId};{order.OrderId};{order.Sum}";


            var byteData = Encoding.UTF8.GetBytes(signDataBeforeEncode);

            var algorithm = SymmetricAlgorithm.Create("TripleDes");
            algorithm.Mode = CipherMode.ECB;
            algorithm.Padding = PaddingMode.PKCS7;

            var encryptor = algorithm.CreateEncryptor(Convert.FromBase64String(merchantKey), new byte[8]);
            string signData = Convert.ToBase64String(encryptor.TransformFinalBlock(byteData, 0, byteData.Length));

            var data = new
            {
                MerchantId= merchantId,
                TerminalId= terminalId,
                Amount= order.Sum,
                OrderId= order.OrderId,
                LocalDateTime=DateTime.Now,
                ReturnUrl= "https://localhost:44358/Home/CallBack",
                SignData= signData
            };


           var res= CallApi<RequestPaymentResult>("https://sadad.shaparak.ir/api/v0/Request/PaymentRequest", data).Result;
           if (res.ResCode==0)
           {
               return Redirect($"https://sadad.shaparak.ir/Purchase/Index?token={res.Token}");
           }
           else
           {
               return View("PaymentError", res);
           }

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