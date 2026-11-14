.syntax unified
.cpu cortex-m4
.thumb
.section .isr_vector,"a",%progbits
.word _estack
.word Reset_Handler
.rept 14
.word Default_Handler
.endr

.section .text.Reset_Handler,"ax",%progbits
.global Reset_Handler
.type Reset_Handler,%function
.thumb_func
Reset_Handler:
  cpsid i
  ldr sp, =_estack
  ldr r0, =_sidata
  ldr r1, =_sdata
  ldr r2, =_edata
1:
  cmp r1, r2
  bcs 2f
  ldr r3, [r0], #4
  str r3, [r1], #4
  b 1b
2:
  ldr r1, =_sbss
  ldr r2, =_ebss
  movs r3, #0
3:
  cmp r1, r2
  bcs 4f
  str r3, [r1], #4
  b 3b
4:
  ldr r0, =0xE000ED08
  ldr r1, =0x08000000
  str r1, [r0]
  bl main
5:
  b 5b
.thumb_func
Default_Handler:
  b Default_Handler
