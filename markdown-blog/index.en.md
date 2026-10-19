# bindeep Lab

Binary program analysis and vulnerability mining. We turn compiled binaries back into source that compiles again, and look for known and unknown defects in it.

<div class="work-card">
<div class="work-head"><span class="work-name">bindec</span><a class="work-link" href="https://bindeep.cn/">bindeep.cn ↗</a></div>
<p class="work-desc">A binary decompiler. What it produces is <strong>source that a compiler accepts directly</strong>, not pseudo-code that has to be patched first; we are working towards making the recompiled program behave exactly like the original.</p>
<p class="work-metrics">Measured head to head against IDA Pro 9.1 and Ghidra 12.1.4 on 128 test programs, feeding the output straight to gcc: bindec compiled 113 of them (88.3%), and for 100 of those (78.1%) the recompiled program behaved like the original. IDA and Ghidra, after we patched them up (regex rules + C++ class shells), reached 57 and 44. The <a href="https://github.com/bindeep-lib/DCB">test dataset is open source</a>; these numbers hold only for this set of programs.</p>
</div>

We have been doing this for a while, and we are looking for people who want to do it with us: joint research, engineering work, or putting bindec to use in real security assessments.

<div class="contact">
<img class="contact-qr" src="images/wechat-qr.jpg" alt="WeChat QR code" width="150">
<div class="contact-text">
<p><strong>WeChat</strong> scan to add us</p>
<p><strong>bindeep.cn</strong> binforge · intelligent decompilation and vulnerability analysis platform</p>
</div>
</div>
