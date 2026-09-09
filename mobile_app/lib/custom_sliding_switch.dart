import 'package:flutter/material.dart';

class CustomSlidingSwitch extends StatelessWidget {
  final String textLeft;
  final String textRight;
  final bool isRightSelected;
  final ValueChanged<bool> onChanged;

  const CustomSlidingSwitch({
    super.key,
    required this.textLeft,
    required this.textRight,
    required this.isRightSelected,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 260,
      height: 45,
      decoration: BoxDecoration(
        color: const Color(0xFF252525),
        borderRadius: BorderRadius.circular(22.5),
        boxShadow: const [
          BoxShadow(color: Colors.black26, blurRadius: 8, offset: Offset(0, 4)),
        ],
      ),
      child: Stack(
        children: [
          AnimatedPositioned(
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeInOut,
            top: 4,
            bottom: 4,
            left: isRightSelected ? 130 : 4,
            right: isRightSelected ? 4 : 130,
            child: Container(
              decoration: BoxDecoration(
                color: Colors.deepPurpleAccent,
                borderRadius: BorderRadius.circular(18.5),
              ),
            ),
          ),
          Row(
            children: [
              Expanded(
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: () => onChanged(false),
                  child: Center(
                    child: Text(textLeft, style: TextStyle(
                      color: !isRightSelected ? Colors.white : Colors.white54,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    )),
                  ),
                ),
              ),
              Expanded(
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: () => onChanged(true),
                  child: Center(
                    child: Text(textRight, style: TextStyle(
                      color: isRightSelected ? Colors.white : Colors.white54,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    )),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
