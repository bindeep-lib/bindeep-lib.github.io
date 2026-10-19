# bindeep 实验室

二进制程序分析与漏洞挖掘。我们把已编译的二进制还原成能重新编译的源码，并在其中寻找已知与未知的缺陷。

<div class="work-card">
<div class="work-head"><span class="work-name">bindec</span><a class="work-link" href="https://bindeep.cn/">bindeep.cn ↗</a></div>
<p class="work-desc">二进制反编译器。产出的是<strong>能直接交给编译器的源码</strong>，而不是需要先修补才能编译的伪代码；我们正在努力让重新编译出来的程序与原程序行为一致。</p>
<p class="work-metrics">在 128 个测试程序上与 IDA Pro 9.1、Ghidra 12.1.4 同场实测，产物原样进 gcc：bindec 有 113 个编译通过（88.3%），其中 100 个（78.1%）运行结果与原程序一致；IDA 与 Ghidra 在经过源码修补后（正则 + C++ class 壳）分别是 57 个和 44 个。测试数据集<a href="https://github.com/bindeep-lib/DCB">已开源</a>，这些数字只在这批测试上成立。</p>
</div>

我们长期做这件事，也在找愿意一起做的合作方：共同研究、工程实现，或者把 bindec 用进真实的安全评估。

<div class="contact">
<img class="contact-qr" src="images/wechat-qr.jpg" alt="微信二维码" width="150">
<div class="contact-text">
<p><strong>微信</strong> 扫码添加</p>
<p><strong>bindeep.cn</strong> binforge · 智能反编译与漏洞分析平台</p>
</div>
</div>
