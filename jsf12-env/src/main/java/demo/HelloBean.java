package demo;

/**
 * JSF 1.2 managed bean（在 faces-config.xml 中注册，session 作用域）。
 * 断点建议：打在 sayHello() 内，触发后即可在远程调试器里命中。
 */
public class HelloBean {

    private String name = "";
    private String message = "";

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getMessage() {
        return message;
    }

    /**
     * commandButton 的 action 方法。返回 null 表示留在当前页。
     */
    public String sayHello() {
        String who = (name == null || name.trim().length() == 0) ? "世界" : name.trim();
        message = "你好，" + who + "！（来自 JSF 1.2 managed bean）";
        return null;
    }
}
