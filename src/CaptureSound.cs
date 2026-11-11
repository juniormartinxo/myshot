using System;
using System.IO;
using System.Media;
using System.Text;

namespace MyShot {
 // Clique curto de obturador, sintetizado em memória. Não depende do esquema de sons do Windows (que pode estar silencioso).
 internal static class CaptureSound {
  const int Rate=22050;
  static byte[] wav;

  internal static void Play(){
   try{
    if(wav==null)wav=Build();
    var player=new SoundPlayer(new MemoryStream(wav));
    player.Play();
   }catch(Exception){}
  }

  static byte[] Build(){
   const double seconds=0.09;
   int n=(int)(Rate*seconds);
   var samples=new short[n];
   // Disparo de câmera: duas batidas mecânicas (espelho e cortina), cada uma um ruído curto com passa-alta,
   // um pouco de ressonância aguda e decaimento rápido. Volume baixo para ser discreto.
   var random=new Random(7);
   double previous=0,lowpass=0;
   for(int i=0;i<n;i++){
    double t=i/(double)Rate;
    double noise=random.NextDouble()*2-1;
    double highpassed=noise-previous;previous=noise;
    lowpass+=0.35*(highpassed-lowpass);
    double first=lowpass*Math.Exp(-t*420);
    double ringing=Math.Sin(2*Math.PI*2600*t)*Math.Exp(-t*160)*0.25;
    double tt=t-0.042;
    double second=tt>=0?lowpass*Math.Exp(-tt*380)*0.8:0;
    double value=(first+ringing+second)*0.42;
    samples[i]=(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,value*12000));
   }
   int dataLength=n*2;
   using(var ms=new MemoryStream())
   using(var w=new BinaryWriter(ms)){
    w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+dataLength);w.Write(Encoding.ASCII.GetBytes("WAVE"));
    w.Write(Encoding.ASCII.GetBytes("fmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
    w.Write(Encoding.ASCII.GetBytes("data"));w.Write(dataLength);
    foreach(var s in samples)w.Write(s);
    w.Flush();
    return ms.ToArray();
   }
  }
 }
}
